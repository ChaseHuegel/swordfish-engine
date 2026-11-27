using System.Diagnostics;
using System.Globalization;
using Swordfish.IO;
using Swordfish.Library.Util;
using WaywardBeyond.AssetStudio.Models;
using WaywardBeyond.Bodies;
using WaywardBeyond.Bricks;
using WaywardBeyond.Items;
using WaywardBeyond.Skills;
using GameBlueprintData = WaywardBeyond.Data.BlueprintData;
using GameContentVersion = WaywardBeyond.Data.ContentVersion;
using ToolsBlueprints = WaywardBeyond.AssetTools.Blueprints;
using ToolsDocuments = WaywardBeyond.AssetTools.Documents;
using ToolsPacks = WaywardBeyond.AssetTools.Packs;
using ToolsStorage = WaywardBeyond.AssetTools.Storage;
using ToolsValidation = WaywardBeyond.AssetTools.Validation;
using ToolsWorkspace = WaywardBeyond.AssetTools.Workspace;

namespace WaywardBeyond.AssetStudio.Services;

/// <summary>
///     Projects a live <see cref="ToolsWorkspace.WorkspaceIndex"/> into UI-ready studio snapshot models.
/// </summary>
internal static class AssetSeedProjector
{
    private static readonly string[] BODY_DIRECTION_SWATCHES = ["#5282B8", "#436B97", "#5B8EC4", "#4D79A8"];
    private static readonly string[] IMAGE_EXTENSIONS = [".png", ".bmp", ".jpg", ".jpeg", ".webp"];
    private static readonly string[] PBR_SIDECAR_SUFFIXES =
    [
        ".m.png", ".s.png", ".n.png", ".e.png", ".t.png",
        ".m.bmp", ".s.bmp", ".n.bmp", ".e.bmp", ".t.bmp",
    ];
    private static readonly HashSet<string> IGNORED_SCAN_DIRECTORIES = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git",
        ".vs",
        ".idea",
        ".assetstudio",
        "bin",
        "obj",
        "node_modules",
    };

    /// <summary>
    ///     Returns whether <paramref name="fileName"/> uses a non-albedo PBR channel sidecar suffix.
    /// </summary>
    public static bool IsPbrChannelSidecar(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        for (var i = 0; i < PBR_SIDECAR_SUFFIXES.Length; i++)
        {
            if (fileName.EndsWith(PBR_SIDECAR_SUFFIXES[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Returns whether <paramref name="rootPath"/> contains pack metadata, a module manifest, or an <c>assets/</c> directory.
    /// </summary>
    public static bool HasPackStructure(string? rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
        {
            return false;
        }

        return File.Exists(Path.Combine(rootPath, ToolsPacks.PackMetadata.FILE_NAME))
            || File.Exists(Path.Combine(rootPath, "manifest.toml"))
            || Directory.Exists(Path.Combine(rootPath, "assets"));
    }

    /// <summary>
    ///     Builds a complete <see cref="StudioWorkspaceSnapshot"/> from an indexed workspace, validation report, and recovery drafts.
    /// </summary>
    public static StudioWorkspaceSnapshot BuildSnapshot(
        ToolsWorkspace.WorkspaceIndex workspace,
        ToolsValidation.ValidationReport report,
        IReadOnlyList<ToolsStorage.DraftRecoveryEntry> recoveryDrafts,
        string? explicitDisplayName = null,
        string? persistedDefaultNamespace = null)
    {
        var studioMode = workspace.Mode == ToolsWorkspace.WorkspaceMode.Repo
            ? WorkspaceMode.Repo
            : WorkspaceMode.Pack;

        var primaryWritable = workspace.GetPrimaryWritableRoot();
        var folderName = GetDirectoryDisplayName(workspace.RootPath);
        ToolsPacks.PackMetadata? packMeta = null;
        if (studioMode == WorkspaceMode.Pack)
        {
            var packDir = primaryWritable.Success ? primaryWritable.Value.RootDirectory : workspace.RootPath;
            var meta = ToolsPacks.PackMetadata.Load(packDir);
            if (meta.Success)
            {
                packMeta = meta.Value;
            }
        }

        string packId;
        string packNamespace;
        if (studioMode == WorkspaceMode.Repo)
        {
            packId = primaryWritable.Success && !string.IsNullOrWhiteSpace(primaryWritable.Value.Id)
                ? primaryWritable.Value.Id
                : "wayward_beyond";
            packNamespace = "wb";
        }
        else
        {
            packId = ResolvePackId(packMeta, primaryWritable, folderName);
            packNamespace = ResolvePackNamespace(packMeta, packId);
        }

        var defaultNamespace = !string.IsNullOrWhiteSpace(persistedDefaultNamespace)
            ? persistedDefaultNamespace.Trim().TrimEnd(':').Trim()
            : packNamespace;

        var workspaceName = ResolveEffectiveWorkspaceDisplayName(
            workspace,
            studioMode,
            packId,
            packMeta,
            primaryWritable,
            explicitDisplayName);
        var isUninitializedFolder = studioMode == WorkspaceMode.Pack && !HasPackStructure(workspace.RootPath);

        var entryCountsByFile = CountAssetEntriesPerFile(workspace);
        var assets = new List<StudioAssetSeed>();

        ProjectStructuredAssets(workspace, studioMode, entryCountsByFile, assets);
        ProjectTagAssets(workspace, studioMode, assets);
        ProjectLocalizationAssets(workspace, studioMode, assets);
        ProjectBlueprintAssets(workspace, studioMode, assets);
        ProjectSyntaxErrorDocuments(workspace, studioMode, assets);

        var findings = ProjectValidationFindings(workspace, report);
        var workspaceImages = ProjectWorkspaceImages(workspace);
        var candidates = ProjectReferenceCandidates(workspace, studioMode, workspaceImages);
        var drafts = ProjectDraftRecoveryEntries(workspace, recoveryDrafts);
        var availableShaders = DiscoverAvailableShaders(workspace);

        return new StudioWorkspaceSnapshot
        {
            WorkspaceName = workspaceName,
            Mode = studioMode,
            RootPath = workspace.RootPath,
            ReferenceRootPath = workspace.ReferenceInstallPath,
            ContentVersion = (int)GameContentVersion.CURRENT,
            PackId = packId,
            PackNamespace = packNamespace,
            DefaultNamespace = defaultNamespace,
            AvailableShaders = availableShaders,
            WorkspaceImages = workspaceImages,
            IsUninitializedFolder = isUninitializedFolder,
            Assets = assets,
            Findings = findings,
            Candidates = candidates,
            PendingDrafts = drafts,
        };
    }

    /// <summary>
    ///     Recursively discovers image files across enabled workspace roots, preferring higher-priority roots on collision.
    /// </summary>
    public static IReadOnlyList<WorkspaceImageEntry> ProjectWorkspaceImages(ToolsWorkspace.WorkspaceIndex workspace)
    {
        var byRelativePath = ScanWorkspaceAssetsByRelativePath(
            workspace,
            preferredAssetsSubdir: "textures",
            static fileName => HasImageExtension(fileName) && !IsPbrChannelSidecar(fileName));

        var entries = new List<WorkspaceImageEntry>(byRelativePath.Values);
        var naturalComparer = new NaturalComparer(StringComparison.OrdinalIgnoreCase);
        entries.Sort((left, right) =>
        {
            var rankCmp = GetWorkspaceImageSortRank(left.RelativePath).CompareTo(GetWorkspaceImageSortRank(right.RelativePath));
            if (rankCmp != 0)
            {
                return rankCmp;
            }

            var cmp = naturalComparer.Compare(left.RelativePath, right.RelativePath);
            return cmp != 0
                ? cmp
                : StringComparer.OrdinalIgnoreCase.Compare(left.RelativePath, right.RelativePath);
        });

        return entries;
    }

    private static Dictionary<string, WorkspaceImageEntry> ScanWorkspaceAssetsByRelativePath(
        ToolsWorkspace.WorkspaceIndex workspace,
        string preferredAssetsSubdir,
        Func<string, bool> includeFilePredicate)
    {
        var byRelativePath = new Dictionary<string, WorkspaceImageEntry>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < workspace.Roots.Count; i++)
        {
            var root = workspace.Roots[i];
            if (!root.IsEnabled)
            {
                continue;
            }

            var scannedDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(root.RootDirectory) && Directory.Exists(root.RootDirectory))
            {
                ScanWorkspaceAssetDirectory(in root, root.RootDirectory, preferredAssetsSubdir, includeFilePredicate, scannedDirs, byRelativePath);
            }

            if (!string.IsNullOrWhiteSpace(root.AssetsDirectory) && Directory.Exists(root.AssetsDirectory))
            {
                ScanWorkspaceAssetDirectory(in root, root.AssetsDirectory, preferredAssetsSubdir, includeFilePredicate, scannedDirs, byRelativePath);
            }
        }

        return byRelativePath;
    }

    /// <summary>
    ///     Projects backend validation findings into studio validation finding view records.
    /// </summary>
    public static IReadOnlyList<ValidationFinding> ProjectValidationFindings(
        ToolsWorkspace.WorkspaceIndex workspace,
        ToolsValidation.ValidationReport report)
    {
        var list = new List<ValidationFinding>(report.Findings.Count);
        for (var i = 0; i < report.Findings.Count; i++)
        {
            var finding = report.Findings[i];
            if (finding.Severity == ToolsValidation.ValidationSeverity.Informational)
            {
                continue;
            }

            var severity = finding.Severity switch
            {
                ToolsValidation.ValidationSeverity.Fatal => ValidationSeverity.Fatal,
                ToolsValidation.ValidationSeverity.Broken => ValidationSeverity.Broken,
                ToolsValidation.ValidationSeverity.CrossPlatform => ValidationSeverity.CrossPlatform,
                _ => ValidationSeverity.Style,
            };

            var rootName = ResolveRootName(workspace, finding.RootId, finding.FilePath);
            var relativeFile = ComputeRelativeFile(workspace, finding.FilePath);
            var assetId = ResolveAssetIdForFile(workspace, finding.FilePath, relativeFile);
            var canFixCase = string.Equals(finding.Code, "PATH001", StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(finding.SuggestedFixValue);

            var quickFixKind = ValidationQuickFixKind.None;
            string? quickFixLabel = null;
            string? targetKey = null;

            if (string.Equals(finding.Code, "LOC001", StringComparison.Ordinal)
                || finding.FixKind == ToolsValidation.ValidationFixKind.CreateLocalizationKey)
            {
                targetKey = !string.IsNullOrWhiteSpace(finding.SuggestedFixValue)
                    ? finding.SuggestedFixValue.Trim()
                    : ExtractFirstSingleQuotedToken(finding.Message);
                quickFixKind = ValidationQuickFixKind.CreateLocalizationKey;
                quickFixLabel = !string.IsNullOrWhiteSpace(finding.FixDescription)
                    ? finding.FixDescription
                    : (!string.IsNullOrWhiteSpace(targetKey)
                        ? $"Create localization key \"{targetKey}\""
                        : "Create localization key...");
            }
            else if (canFixCase)
            {
                quickFixKind = ValidationQuickFixKind.FixPathCase;
                quickFixLabel = !string.IsNullOrWhiteSpace(finding.FixDescription)
                    ? finding.FixDescription
                    : $"Fix path casing (\"{finding.SuggestedFixValue}\")";
            }
            else if (finding.FixKind == ToolsValidation.ValidationFixKind.StripUtf8Bom)
            {
                quickFixKind = ValidationQuickFixKind.StripUtf8Bom;
                quickFixLabel = !string.IsNullOrWhiteSpace(finding.FixDescription)
                    ? finding.FixDescription
                    : "Strip UTF-8 BOM";
            }
            else if (finding.FixKind == ToolsValidation.ValidationFixKind.NormalizeLineEndings)
            {
                quickFixKind = ValidationQuickFixKind.NormalizeLineEndings;
                quickFixLabel = !string.IsNullOrWhiteSpace(finding.FixDescription)
                    ? finding.FixDescription
                    : "Normalize line endings to LF";
            }
            else if (finding.FixKind == ToolsValidation.ValidationFixKind.RemoveArrayElement)
            {
                quickFixKind = ValidationQuickFixKind.RemoveArrayElement;
                quickFixLabel = !string.IsNullOrWhiteSpace(finding.FixDescription)
                    ? finding.FixDescription
                    : "Remove invalid array entry";
            }
            else if (finding.FixKind == ToolsValidation.ValidationFixKind.InsertDefaultSkillLevels)
            {
                quickFixKind = ValidationQuickFixKind.InsertDefaultSkillLevels;
                quickFixLabel = !string.IsNullOrWhiteSpace(finding.FixDescription)
                    ? finding.FixDescription
                    : "Insert default skill level 1 = 100";
            }
            else if (finding.FixKind == ToolsValidation.ValidationFixKind.RemoveField)
            {
                quickFixKind = ValidationQuickFixKind.RemoveField;
                quickFixLabel = !string.IsNullOrWhiteSpace(finding.FixDescription)
                    ? finding.FixDescription
                    : "Remove conflicting field";
            }
            else if (finding.FixKind == ToolsValidation.ValidationFixKind.ReplaceValue
                     && !string.IsNullOrWhiteSpace(finding.SuggestedFixValue))
            {
                quickFixKind = ValidationQuickFixKind.ReplaceValue;
                quickFixLabel = !string.IsNullOrWhiteSpace(finding.FixDescription)
                    ? finding.FixDescription
                    : $"Replace with \"{finding.SuggestedFixValue}\"";
            }
            else if (!string.IsNullOrWhiteSpace(finding.SuggestedFixValue))
            {
                quickFixKind = ValidationQuickFixKind.ReplaceReference;
                quickFixLabel = !string.IsNullOrWhiteSpace(finding.FixDescription)
                    ? finding.FixDescription
                    : $"Replace with \"{finding.SuggestedFixValue}\"";
            }
            else if (IsFormattingFinding(in finding))
            {
                quickFixKind = ValidationQuickFixKind.FormatFile;
                quickFixLabel = !string.IsNullOrWhiteSpace(finding.FixDescription)
                    ? finding.FixDescription
                    : "Format file";
            }

            list.Add(new ValidationFinding(
                Id: $"{finding.Code}-{i + 1:000}",
                Severity: severity,
                RootName: rootName,
                RelativeFile: relativeFile,
                LineNumber: Math.Max(1, finding.Line),
                AssetId: assetId,
                FieldKey: MapFindingFieldKey(finding.Field),
                Message: finding.Message,
                CanFixCase: canFixCase,
                SuggestedFix: finding.SuggestedFixValue,
                Code: finding.Code,
                QuickFixKind: quickFixKind,
                QuickFixLabel: quickFixLabel,
                TargetKey: targetKey,
                IsSafeBulkFix: finding.IsSafeBulkFix,
                FixDescription: finding.FixDescription,
                AbsoluteFilePath: finding.FilePath,
                BackendFindingIndex: i));
        }

        return list;
    }

    /// <summary>
    ///     Maps a studio asset kind to its backend workspace asset kind.
    /// </summary>
    public static ToolsWorkspace.AssetKind ToToolsAssetKind(AssetKind kind)
    {
        return kind switch
        {
            AssetKind.Brick => ToolsWorkspace.AssetKind.Brick,
            AssetKind.Item => ToolsWorkspace.AssetKind.Item,
            AssetKind.Paint => ToolsWorkspace.AssetKind.Paint,
            AssetKind.Skill => ToolsWorkspace.AssetKind.Skill,
            AssetKind.Body => ToolsWorkspace.AssetKind.Body,
            AssetKind.Material => ToolsWorkspace.AssetKind.Material,
            AssetKind.Tag => ToolsWorkspace.AssetKind.Tag,
            AssetKind.Localization => ToolsWorkspace.AssetKind.Localization,
            AssetKind.Blueprint => ToolsWorkspace.AssetKind.Blueprint,
            _ => ToolsWorkspace.AssetKind.Brick,
        };
    }

    /// <summary>
    ///     Maps a backend workspace asset kind to its studio UI asset kind.
    /// </summary>
    public static AssetKind ToStudioAssetKind(ToolsWorkspace.AssetKind kind)
    {
        return kind switch
        {
            ToolsWorkspace.AssetKind.Brick => AssetKind.Brick,
            ToolsWorkspace.AssetKind.Item => AssetKind.Item,
            ToolsWorkspace.AssetKind.Paint => AssetKind.Paint,
            ToolsWorkspace.AssetKind.Skill => AssetKind.Skill,
            ToolsWorkspace.AssetKind.Body => AssetKind.Body,
            ToolsWorkspace.AssetKind.Material => AssetKind.Material,
            ToolsWorkspace.AssetKind.Tag => AssetKind.Tag,
            ToolsWorkspace.AssetKind.Localization => AssetKind.Localization,
            ToolsWorkspace.AssetKind.Blueprint => AssetKind.Blueprint,
            _ => AssetKind.Brick,
        };
    }

    private static void ProjectStructuredAssets(
        ToolsWorkspace.WorkspaceIndex workspace,
        WorkspaceMode studioMode,
        Dictionary<string, int> entryCountsByFile,
        List<StudioAssetSeed> output)
    {
        (ToolsWorkspace.AssetKind ToolsKind, AssetKind StudioKind, string GroupTitle)[] orderedKinds =
        [
            (ToolsWorkspace.AssetKind.Brick, AssetKind.Brick, "Bricks"),
            (ToolsWorkspace.AssetKind.Item, AssetKind.Item, "Items"),
            (ToolsWorkspace.AssetKind.Paint, AssetKind.Paint, "Paints"),
            (ToolsWorkspace.AssetKind.Skill, AssetKind.Skill, "Skills"),
            (ToolsWorkspace.AssetKind.Body, AssetKind.Body, "Bodies"),
            (ToolsWorkspace.AssetKind.Material, AssetKind.Material, "Materials"),
        ];

        for (var k = 0; k < orderedKinds.Length; k++)
        {
            var (toolsKind, studioKind, groupTitle) = orderedKinds[k];
            var winning = workspace.GetWinningAssetsByKind(toolsKind);
            for (var i = 0; i < winning.Count; i++)
            {
                var asset = winning[i];
                var seed = BuildStructuredAssetSeed(workspace, studioMode, studioKind, groupTitle, in asset, entryCountsByFile);
                output.Add(seed);
            }
        }
    }

    private static StudioAssetSeed BuildStructuredAssetSeed(
        ToolsWorkspace.WorkspaceIndex workspace,
        WorkspaceMode studioMode,
        AssetKind studioKind,
        string groupTitle,
        in ToolsWorkspace.IndexedAsset asset,
        Dictionary<string, int> entryCountsByFile)
    {
        var prov = asset.Provenance;
        var studioProv = MapProvenance(studioMode, in prov);
        var shadowedRoot = prov.IsOverride ? prov.ShadowedProviders[^1].RootName : null;

        string? multiEntryLabel = null;
        if (entryCountsByFile.TryGetValue(prov.FilePath, out var entryCount) && entryCount > 1)
        {
            multiEntryLabel = $"{Path.GetFileName(prov.FilePath)} · {entryCount}";
        }

        string? rawToml = null;
        string? baseHash = null;
        if (workspace.DocumentsByPath.TryGetValue(prov.FilePath, out var tomlDoc))
        {
            rawToml = tomlDoc.Text;
            baseHash = ToolsStorage.ContentHash.ComputeSha256Hex(tomlDoc.ToBytes());
        }

        var scalars = new Dictionary<string, string>(StringComparer.Ordinal);
        var colliders = new List<(string Type, string Size, string Offset, string Rotation)>();
        var skillSources = new List<(string Trigger, string Target, int Xp)>();
        var skillLevels = new List<(int Level, int XpRequired)>();
        var bodySprites = new List<(string State, string Direction, string TexturePath, string SwatchHex)>();
        var bodySpriteAbsPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? bodyFrontPath = null;
        string? materialTexturePath = null;
        string? paintTexturePath = null;

        switch (asset.AuthoritativeDefinition)
        {
            case BrickDefinition brick:
                PopulateBrickFields(in brick, scalars, colliders);
                break;
            case ItemDefinition item:
                PopulateItemFields(in item, scalars);
                break;
            case PaintDefinition paint:
                paintTexturePath = PopulatePaintFields(workspace, in paint, scalars);
                break;
            case SkillDefinition skill:
                PopulateSkillFields(in skill, scalars, skillSources, skillLevels);
                break;
            case BodyDefinition body:
                bodyFrontPath = PopulateBodyFields(workspace, in body, scalars, bodySprites, bodySpriteAbsPaths);
                break;
            case MaterialDefinition material:
                materialTexturePath = PopulateMaterialFields(workspace, in material, scalars);
                break;
        }

        var inbound = ProjectInboundReferences(workspace, asset.Id);
        var relativeFile = string.IsNullOrWhiteSpace(prov.RelativePath)
            ? ComputeRelativeFile(workspace, prov.FilePath)
            : "assets/" + prov.RelativePath.TrimStart('/');

        return new StudioAssetSeed
        {
            Id = asset.Id,
            Kind = studioKind,
            GroupTitle = groupTitle,
            RelativeFile = relativeFile,
            RootName = prov.ProviderRootName,
            Provenance = studioProv,
            ShadowedRoot = shadowedRoot,
            MultiEntryLabel = multiEntryLabel,
            RawTomlOverride = rawToml,
            AbsoluteFilePath = prov.FilePath,
            TableIndex = prov.TableIndex,
            EntryIndex = prov.EntryIndex,
            BaseContentSha256 = baseHash,
            ScalarValues = scalars,
            Colliders = colliders,
            SkillSources = skillSources,
            SkillLevels = skillLevels,
            BodySprites = bodySprites,
            BodySpriteAbsolutePaths = bodySpriteAbsPaths,
            ResolvedBodyFrontTexturePath = bodyFrontPath,
            ResolvedMaterialTexturePath = materialTexturePath,
            ResolvedPaintTexturePath = paintTexturePath,
            InboundReferences = inbound,
        };
    }

    private static void PopulateBrickFields(
        in BrickDefinition brick,
        Dictionary<string, string> scalars,
        List<(string Type, string Size, string Offset, string Rotation)> colliders)
    {
        scalars["Shape"] = brick.Shape.ToString();
        scalars["Color"] = NormalizeHexOrDefault(brick.Color, "#FFFFFF");
        scalars["Alpha"] = brick.Alpha.ToString();
        scalars["CastShadows"] = brick.CastShadows ? "true" : "false";
        scalars["Passable"] = brick.Passable ? "true" : "false";
        scalars["Mesh"] = brick.Mesh ?? string.Empty;
        scalars["Connected"] = brick.Textures.Connected ? "true" : "false";
        scalars["TextureDefault"] = JoinNullableArray(brick.Textures.Default);
        scalars["TextureTop"] = JoinNullableArray(brick.Textures.Top);
        scalars["TextureBottom"] = JoinNullableArray(brick.Textures.Bottom);
        scalars["TextureFront"] = JoinNullableArray(brick.Textures.Front);
        scalars["TextureBack"] = JoinNullableArray(brick.Textures.Back);
        scalars["TextureLeft"] = JoinNullableArray(brick.Textures.Left);
        scalars["TextureRight"] = JoinNullableArray(brick.Textures.Right);

        if (brick.Sounds != null)
        {
            if (brick.Sounds.TryGetValue(BrickEventKind.Place, out var placeSounds))
            {
                scalars["SoundPlace"] = JoinNullableArray(placeSounds);
            }

            if (brick.Sounds.TryGetValue(BrickEventKind.Break, out var breakSounds))
            {
                scalars["SoundBreak"] = JoinNullableArray(breakSounds);
            }
        }

        if (brick.Colliders != null)
        {
            for (var i = 0; i < brick.Colliders.Count; i++)
            {
                var c = brick.Colliders[i];
                var type = string.IsNullOrWhiteSpace(c.Type) ? "box" : c.Type;
                var size = FormatFloat3(c.Size.X, c.Size.Y, c.Size.Z);
                var offset = FormatFloat3(c.Offset.X, c.Offset.Y, c.Offset.Z);
                var rot = FormatFloat3(c.Rotation.X, c.Rotation.Y, c.Rotation.Z);
                colliders.Add((type, size, offset, rot));
            }
        }
    }

    private static void PopulateItemFields(in ItemDefinition item, Dictionary<string, string> scalars)
    {
        scalars["Name"] = item.Name ?? string.Empty;
        scalars["Description"] = item.Description ?? string.Empty;
        scalars["Icon"] = item.Icon ?? string.Empty;
        scalars["MaxStack"] = (item.MaxStack ?? 64).ToString(CultureInfo.InvariantCulture);

        if (item.Placeable.HasValue)
        {
            scalars["PlaceableType"] = item.Placeable.Value.Type.ToString().ToLowerInvariant();
            scalars["PlaceableID"] = item.Placeable.Value.ID ?? string.Empty;
        }
        else
        {
            scalars["PlaceableType"] = "none";
            scalars["PlaceableID"] = string.Empty;
        }

        if (item.Tool.HasValue)
        {
            scalars["ToolType"] = item.Tool.Value.Type.ToString().ToLowerInvariant();
            scalars["ToolTarget"] = item.Tool.Value.Target ?? string.Empty;
        }
        else
        {
            scalars["ToolType"] = "none";
            scalars["ToolTarget"] = string.Empty;
        }

        if (item.ViewModel.HasValue)
        {
            var vm = item.ViewModel.Value;
            scalars["ViewModelMesh"] = vm.Mesh ?? string.Empty;
            scalars["ViewModelMaterial"] = vm.Material ?? string.Empty;
            scalars["ViewModelPosition"] = FormatFloat3(vm.Position.X, vm.Position.Y, vm.Position.Z);
            scalars["ViewModelRotation"] = FormatFloat3(vm.Rotation.X, vm.Rotation.Y, vm.Rotation.Z);
            scalars["ViewModelScale"] = FormatFloat3(vm.Scale.X, vm.Scale.Y, vm.Scale.Z);
        }

        if (item.WorldModel.HasValue)
        {
            var wm = item.WorldModel.Value;
            scalars["WorldModelMesh"] = wm.Mesh ?? string.Empty;
            scalars["WorldModelMaterial"] = wm.Material ?? string.Empty;
            scalars["WorldModelPosition"] = FormatFloat3(wm.Position.X, wm.Position.Y, wm.Position.Z);
            scalars["WorldModelRotation"] = FormatFloat3(wm.Rotation.X, wm.Rotation.Y, wm.Rotation.Z);
            scalars["WorldModelScale"] = FormatFloat3(wm.Scale.X, wm.Scale.Y, wm.Scale.Z);
        }
    }

    private static string? PopulatePaintFields(
        ToolsWorkspace.WorkspaceIndex workspace,
        in PaintDefinition paint,
        Dictionary<string, string> scalars)
    {
        var isTexture = paint.Textures != null && paint.Textures.Length > 0;
        scalars["Name"] = paint.Name ?? string.Empty;
        scalars["PaintKind"] = isTexture ? "Texture" : "Color";
        scalars["Color"] = NormalizeHexOrDefault(paint.Color, "#FFFFFF");
        scalars["Alpha"] = paint.Alpha.ToString();
        scalars["Connected"] = paint.Connected ? "true" : "false";
        scalars["Variants"] = JoinNullableArray(paint.Textures);

        if (!isTexture || paint.Textures == null)
        {
            return null;
        }

        var firstLayer = FirstNonEmpty(paint.Textures);
        return ResolvePaintTextureDiskPath(workspace, firstLayer, paint.Connected);
    }

    private static void PopulateSkillFields(
        in SkillDefinition skill,
        Dictionary<string, string> scalars,
        List<(string Trigger, string Target, int Xp)> sources,
        List<(int Level, int XpRequired)> levels)
    {
        scalars["Name"] = skill.Name ?? string.Empty;
        scalars["Category"] = skill.Category ?? string.Empty;
        scalars["Icon"] = skill.Icon ?? string.Empty;

        if (skill.Sources != null)
        {
            foreach (var sourceGroup in skill.Sources.OrderBy(static kvp => kvp.Key.ToString(), StringComparer.OrdinalIgnoreCase))
            {
                if (sourceGroup.Value == null)
                {
                    continue;
                }

                foreach (var target in sourceGroup.Value.OrderBy(static kvp => kvp.Key, StringComparer.OrdinalIgnoreCase))
                {
                    sources.Add((sourceGroup.Key.ToString(), target.Key, target.Value));
                }
            }
        }

        if (skill.Levels != null)
        {
            foreach (var lvl in skill.Levels.OrderBy(static kvp => kvp.Key))
            {
                levels.Add((lvl.Key, lvl.Value));
            }
        }
    }

    private static string? PopulateBodyFields(
        ToolsWorkspace.WorkspaceIndex workspace,
        in BodyDefinition body,
        Dictionary<string, string> scalars,
        List<(string State, string Direction, string TexturePath, string SwatchHex)> sprites,
        Dictionary<string, string> absPaths)
    {
        scalars["Species"] = body.Species ?? string.Empty;
        scalars["Gender"] = body.Gender.ToString();

        string? frontPath = null;
        if (body.States == null)
        {
            return frontPath;
        }

        string[] canonicalDirs = ["front", "back", "left", "right"];
        foreach (var stateEntry in body.States.OrderBy(static kvp => kvp.Key, StringComparer.OrdinalIgnoreCase))
        {
            var stateName = stateEntry.Key;
            var dirMap = stateEntry.Value;
            if (dirMap == null)
            {
                continue;
            }

            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var d = 0; d < canonicalDirs.Length; d++)
            {
                var dir = canonicalDirs[d];
                if (!TryGetCaseInsensitive(dirMap, dir, out var paths) || paths == null || paths.Length == 0)
                {
                    continue;
                }

                visited.Add(dir);
                var joinedTexPath = JoinNullableArray(paths);
                var previewTexPath = FirstNonEmpty(paths);
                sprites.Add((stateName, dir, joinedTexPath, BODY_DIRECTION_SWATCHES[d % BODY_DIRECTION_SWATCHES.Length]));
                var resolved = ResolveTextureDiskPath(workspace, previewTexPath);
                if (resolved != null)
                {
                    absPaths[$"{stateName}:{dir}"] = resolved;
                    if (frontPath == null && string.Equals(dir, "front", StringComparison.OrdinalIgnoreCase))
                    {
                        frontPath = resolved;
                    }
                }
            }

            foreach (var extra in dirMap.OrderBy(static kvp => kvp.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (visited.Contains(extra.Key) || extra.Value == null || extra.Value.Length == 0)
                {
                    continue;
                }

                var joinedTexPath = JoinNullableArray(extra.Value);
                var previewTexPath = FirstNonEmpty(extra.Value);
                sprites.Add((stateName, extra.Key, joinedTexPath, BODY_DIRECTION_SWATCHES[0]));
                var resolved = ResolveTextureDiskPath(workspace, previewTexPath);
                if (resolved != null)
                {
                    absPaths[$"{stateName}:{extra.Key}"] = resolved;
                    frontPath ??= resolved;
                }
            }
        }

        return frontPath;
    }

    private static string? PopulateMaterialFields(
        ToolsWorkspace.WorkspaceIndex workspace,
        in MaterialDefinition material,
        Dictionary<string, string> scalars)
    {
        scalars["Shader"] = material.Shader ?? string.Empty;
        scalars["Textures"] = JoinNullableArray(material.Textures);
        scalars["AlphaMode"] = material.AlphaMode.ToString();
        scalars["AlphaCutoff"] = material.AlphaCutoff.ToString("0.###", CultureInfo.InvariantCulture);

        if (material.Textures == null || material.Textures.Length == 0)
        {
            return null;
        }

        var firstTex = FirstNonEmpty(material.Textures);
        return ResolveTextureDiskPath(workspace, firstTex);
    }

    private static void ProjectTagAssets(
        ToolsWorkspace.WorkspaceIndex workspace,
        WorkspaceMode studioMode,
        List<StudioAssetSeed> output)
    {
        var grouped = new Dictionary<string, List<ToolsWorkspace.IndexedTagEntry>>(StringComparer.Ordinal);
        for (var i = 0; i < workspace.TagEntries.Count; i++)
        {
            var entry = workspace.TagEntries[i];
            if (!grouped.TryGetValue(entry.TagName, out var list))
            {
                list = [];
                grouped[entry.TagName] = list;
            }

            list.Add(entry);
        }

        foreach (var kvp in grouped.OrderBy(static g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            var entries = kvp.Value;
            var primary = entries[^1];
            var baseValues = new List<string>();
            for (var i = 0; i < entries.Count - 1; i++)
            {
                baseValues.AddRange(entries[i].Values);
            }

            string? rawToml = null;
            string? baseHash = null;
            if (workspace.DocumentsByPath.TryGetValue(primary.FilePath, out var doc))
            {
                rawToml = doc.Text;
                baseHash = ToolsStorage.ContentHash.ComputeSha256Hex(doc.ToBytes());
            }

            var prov = studioMode == WorkspaceMode.Repo
                ? AssetProvenance.Repo
                : (primary.IsWritable
                    ? (entries.Count > 1 ? AssetProvenance.Override : AssetProvenance.Pack)
                    : AssetProvenance.Base);

            output.Add(new StudioAssetSeed
            {
                Id = primary.TagName,
                Kind = AssetKind.Tag,
                GroupTitle = "Tags",
                RelativeFile = ComputeRelativeFile(workspace, primary.FilePath),
                RootName = primary.RootName,
                Provenance = prov,
                ShadowedRoot = entries.Count > 1 ? entries[0].RootName : null,
                RawTomlOverride = rawToml,
                AbsoluteFilePath = primary.FilePath,
                BaseContentSha256 = baseHash,
                TagBaseValues = baseValues,
                TagFileValues = [.. primary.Values],
                InboundReferences = ProjectInboundReferences(workspace, $"tag:{primary.TagName}"),
            });
        }
    }

    private static void ProjectLocalizationAssets(
        ToolsWorkspace.WorkspaceIndex workspace,
        WorkspaceMode studioMode,
        List<StudioAssetSeed> output)
    {
        var byFile = new Dictionary<string, List<ToolsWorkspace.IndexedLocalizationEntry>>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < workspace.LocalizationEntries.Count; i++)
        {
            var entry = workspace.LocalizationEntries[i];
            if (!byFile.TryGetValue(entry.FilePath, out var list))
            {
                list = [];
                byFile[entry.FilePath] = list;
            }

            list.Add(entry);
        }

        foreach (var kvp in byFile.OrderBy(static f => f.Key, StringComparer.OrdinalIgnoreCase))
        {
            var filePath = kvp.Key;
            var entries = kvp.Value;
            if (entries.Count == 0)
            {
                continue;
            }

            var first = entries[0];
            var relativeFile = ComputeRelativeFile(workspace, filePath);
            var id = relativeFile.StartsWith("assets/", StringComparison.OrdinalIgnoreCase)
                ? relativeFile.Substring("assets/".Length)
                : relativeFile;

            string? rawToml = null;
            string? baseHash = null;
            if (workspace.DocumentsByPath.TryGetValue(filePath, out var doc))
            {
                rawToml = doc.Text;
                baseHash = ToolsStorage.ContentHash.ComputeSha256Hex(doc.ToBytes());
            }

            var rows = new List<(string Key, string English, string Pseudo)>(entries.Count);
            for (var i = 0; i < entries.Count; i++)
            {
                var row = entries[i];
                rows.Add((row.Key, row.Value, $"[!! {row.Value} ··· !!]"));
            }

            var prov = studioMode == WorkspaceMode.Repo
                ? AssetProvenance.Repo
                : (first.IsWritable ? AssetProvenance.Pack : AssetProvenance.Base);

            output.Add(new StudioAssetSeed
            {
                Id = id,
                Kind = AssetKind.Localization,
                GroupTitle = "Localization",
                RelativeFile = relativeFile,
                RootName = first.RootName,
                Provenance = prov,
                RawTomlOverride = rawToml,
                AbsoluteFilePath = filePath,
                BaseContentSha256 = baseHash,
                LocalizationEntries = rows,
            });
        }
    }

    private static void ProjectBlueprintAssets(
        ToolsWorkspace.WorkspaceIndex workspace,
        WorkspaceMode studioMode,
        List<StudioAssetSeed> output)
    {
        for (var i = 0; i < workspace.BlueprintFiles.Count; i++)
        {
            var fullPath = workspace.BlueprintFiles[i];
            var ownerRoot = FindOwningRoot(workspace, fullPath);
            var blueprintsDir = ownerRoot.HasValue
                ? Path.Combine(ownerRoot.Value.AssetsDirectory, "blueprints")
                : Path.GetDirectoryName(fullPath) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(blueprintsDir) || !Directory.Exists(blueprintsDir))
            {
                continue;
            }

            var relativeId = Path.GetRelativePath(blueprintsDir, fullPath).Replace('\\', '/');
            if (relativeId.EndsWith(".wbp", StringComparison.OrdinalIgnoreCase))
            {
                relativeId = relativeId.Substring(0, relativeId.Length - 4);
            }

            var manager = new ToolsBlueprints.PackBlueprintManager(blueprintsDir);
            var loaded = manager.Load(relativeId);
            if (!loaded.Success)
            {
                continue;
            }

            var bp = loaded.Value;
            var fileInfo = new FileInfo(fullPath);
            var prov = studioMode == WorkspaceMode.Repo
                ? AssetProvenance.Repo
                : (ownerRoot.HasValue && ownerRoot.Value.IsWritable ? AssetProvenance.Pack : AssetProvenance.Base);

            var (paletteItems, references, brickPaletteCount, paintPaletteCount, legacyRawIdCount, solidVoxelCount) =
                ExtractBlueprintPalette(workspace, relativeId, in bp);
            var inboundBlueprintRefs = workspace.GetInboundReferences(relativeId);
            for (var r = 0; r < inboundBlueprintRefs.Count; r++)
            {
                var inbound = inboundBlueprintRefs[r];
                references.Add(new InboundReferenceItem(
                    SourceAssetId: inbound.SourceAssetId,
                    SourceKind: ToStudioAssetKind(inbound.SourceKind),
                    FieldPath: inbound.FieldName,
                    SourceFile: ComputeRelativeFile(workspace, inbound.SourceFilePath)));
            }
            var effectiveBrickCount = bp.BrickCount > 0 ? bp.BrickCount : solidVoxelCount;
            var brickCountLabel = FormatBlueprintBrickCountLabel(
                effectiveBrickCount,
                brickPaletteCount,
                paintPaletteCount,
                legacyRawIdCount);

            output.Add(new StudioAssetSeed
            {
                Id = relativeId,
                Kind = AssetKind.Blueprint,
                GroupTitle = "Blueprints",
                RelativeFile = ComputeRelativeFile(workspace, fullPath),
                RootName = ownerRoot.HasValue ? ownerRoot.Value.Name : "workspace",
                Provenance = prov,
                AbsoluteFilePath = fullPath,
                BlueprintPalette = paletteItems,
                InboundReferences = references,
                ScalarValues = new Dictionary<string, string>
                {
                    ["BlueprintName"] = bp.Name ?? relativeId,
                    ["SizeLabel"] = $"{bp.SizeX} × {bp.SizeY} × {bp.SizeZ} voxels",
                    ["BrickCountLabel"] = brickCountLabel,
                    ["FileSizeLabel"] = $"{fileInfo.Length / 1024.0:0.0} KiB",
                    ["CreatedAtLabel"] = FormatUnixMs(bp.CreatedAtMs),
                    ["ModifiedAtLabel"] = FormatUnixMs(bp.ModifiedAtMs),
                },
            });
        }
    }

    private static void ProjectSyntaxErrorDocuments(
        ToolsWorkspace.WorkspaceIndex workspace,
        WorkspaceMode studioMode,
        List<StudioAssetSeed> output)
    {
        var existingPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < output.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(output[i].AbsoluteFilePath))
            {
                existingPaths.Add(output[i].AbsoluteFilePath!);
            }
        }

        foreach (var kvp in workspace.DocumentsByPath.OrderBy(static d => d.Key, StringComparer.OrdinalIgnoreCase))
        {
            var doc = kvp.Value;
            if (!doc.HasSyntaxErrors || existingPaths.Contains(kvp.Key))
            {
                continue;
            }

            var rel = ComputeRelativeFile(workspace, kvp.Key);
            var owner = FindOwningRoot(workspace, kvp.Key);
            var errLine = doc.Diagnostics.Count > 0 ? Math.Max(1, doc.Diagnostics[0].Span.Line) : 1;
            var errMsg = doc.Diagnostics.Count > 0
                ? $"Line {errLine}: {doc.Diagnostics[0].Message} — Form view disabled until fixed."
                : "TOML syntax error — Form view disabled until fixed.";

            var prov = studioMode == WorkspaceMode.Repo
                ? AssetProvenance.Repo
                : (owner.HasValue && owner.Value.IsWritable ? AssetProvenance.Pack : AssetProvenance.Base);

            output.Add(new StudioAssetSeed
            {
                Id = Path.GetFileNameWithoutExtension(kvp.Key),
                Kind = InferAssetKindFromPath(rel),
                GroupTitle = InferGroupTitle(InferAssetKindFromPath(rel)),
                RelativeFile = rel,
                RootName = owner.HasValue ? owner.Value.Name : "workspace",
                Provenance = prov,
                HasSyntaxError = true,
                SyntaxErrorLineNumber = errLine,
                SyntaxErrorMessage = errMsg,
                RawTomlOverride = doc.Text,
                AbsoluteFilePath = kvp.Key,
                BaseContentSha256 = ToolsStorage.ContentHash.ComputeSha256Hex(doc.ToBytes()),
            });
        }
    }

    private static List<ReferenceCandidate> ProjectReferenceCandidates(
        ToolsWorkspace.WorkspaceIndex workspace,
        WorkspaceMode studioMode,
        IReadOnlyList<WorkspaceImageEntry> workspaceImages)
    {
        var list = new List<ReferenceCandidate>();

        foreach (var brick in workspace.GetWinningAssetsByKind(ToolsWorkspace.AssetKind.Brick))
        {
            var p = brick.Provenance;
            list.Add(new ReferenceCandidate(
                brick.Id,
                ReferenceKind.BrickId,
                MapProvenance(studioMode, in p),
                ReferenceResolutionState.Resolved,
                p.ProviderRootName,
                p.RelativePath));
        }

        var sortedBlueprints = new List<KeyValuePair<string, string>>(workspace.WinningBlueprintFiles);
        sortedBlueprints.Sort(static (left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.Key, right.Key));
        for (var i = 0; i < sortedBlueprints.Count; i++)
        {
            var blueprintId = sortedBlueprints[i].Key;
            var fullPath = sortedBlueprints[i].Value;
            var ownerRoot = FindOwningRoot(workspace, fullPath);
            var prov = studioMode == WorkspaceMode.Repo
                ? AssetProvenance.Repo
                : (ownerRoot.HasValue && ownerRoot.Value.IsWritable ? AssetProvenance.Pack : AssetProvenance.Base);
            list.Add(new ReferenceCandidate(
                blueprintId,
                ReferenceKind.BlueprintId,
                prov,
                ReferenceResolutionState.Resolved,
                ownerRoot.HasValue ? ownerRoot.Value.Name : "workspace",
                ComputeRelativeFile(workspace, fullPath)));
        }

        foreach (var mat in workspace.GetWinningAssetsByKind(ToolsWorkspace.AssetKind.Material))
        {
            var p = mat.Provenance;
            string? matThumb = null;
            if (mat.AuthoritativeDefinition is MaterialDefinition matDef
                && matDef.Textures != null
                && matDef.Textures.Length > 0)
            {
                matThumb = ResolveTextureDiskPath(workspace, FirstNonEmpty(matDef.Textures));
            }

            list.Add(new ReferenceCandidate(
                mat.Id,
                ReferenceKind.Material,
                MapProvenance(studioMode, in p),
                ReferenceResolutionState.Resolved,
                p.ProviderRootName,
                p.RelativePath,
                ThumbnailPath: matThumb));
        }

        foreach (var layerName in workspace.OrderedAlbedoLayerNames)
        {
            if (string.Equals(layerName, "missing", StringComparison.Ordinal))
            {
                continue;
            }

            if (workspace.WinningBlockAlbedoLayers.TryGetValue(layerName, out var res))
            {
                list.Add(new ReferenceCandidate(
                    layerName,
                    ReferenceKind.BrickFace,
                    MapResourceProvenance(studioMode, in res),
                    ReferenceResolutionState.Resolved,
                    res.ProviderRootName,
                    $"textures/{res.RelativePath}",
                    ThumbnailPath: res.FilePath));
            }
        }

        var primaryTextureCandidates = new List<ReferenceCandidate>();
        var sidecarTextureCandidates = new List<ReferenceCandidate>();
        var seenTextureIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var meshCandidates = new List<ReferenceCandidate>();
        var seenMeshIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var kvp in workspace.WinningResources.OrderBy(static r => r.Key.Id, StringComparer.OrdinalIgnoreCase))
        {
            var res = kvp.Value;
            ReferenceKind? refKind = res.Kind switch
            {
                ToolsWorkspace.ResourceKind.Texture => ReferenceKind.Texture,
                ToolsWorkspace.ResourceKind.Mesh => ReferenceKind.Mesh,
                ToolsWorkspace.ResourceKind.Audio => ReferenceKind.Audio,
                _ => null,
            };

            if (!refKind.HasValue)
            {
                continue;
            }

            if (refKind.Value == ReferenceKind.Texture)
            {
                if (!seenTextureIds.Add(res.Id))
                {
                    continue;
                }

                var texCandidate = new ReferenceCandidate(
                    res.Id,
                    ReferenceKind.Texture,
                    MapResourceProvenance(studioMode, in res),
                    ReferenceResolutionState.Resolved,
                    res.ProviderRootName,
                    res.RelativePath,
                    ThumbnailPath: res.FilePath);

                if (IsPbrChannelSidecar(res.Id))
                {
                    sidecarTextureCandidates.Add(texCandidate);
                }
                else
                {
                    primaryTextureCandidates.Add(texCandidate);
                }
            }
            else if (refKind.Value == ReferenceKind.Mesh)
            {
                if (!seenMeshIds.Add(res.Id))
                {
                    continue;
                }

                var resolvedMeshPath = !string.IsNullOrWhiteSpace(res.FilePath) && File.Exists(res.FilePath)
                    ? res.FilePath
                    : null;
                meshCandidates.Add(new ReferenceCandidate(
                    res.Id,
                    ReferenceKind.Mesh,
                    MapResourceProvenance(studioMode, in res),
                    ReferenceResolutionState.Resolved,
                    res.ProviderRootName,
                    res.RelativePath,
                    ThumbnailPath: resolvedMeshPath));
            }
            else
            {
                list.Add(new ReferenceCandidate(
                    res.Id,
                    refKind.Value,
                    MapResourceProvenance(studioMode, in res),
                    ReferenceResolutionState.Resolved,
                    res.ProviderRootName,
                    res.RelativePath,
                    ThumbnailPath: null));
            }
        }

        var discoveredMeshes = ScanWorkspaceAssetsByRelativePath(
            workspace,
            preferredAssetsSubdir: "meshes",
            static fileName => fileName.EndsWith(".obj", StringComparison.OrdinalIgnoreCase));

        foreach (var mesh in discoveredMeshes.Values)
        {
            if (string.IsNullOrWhiteSpace(mesh.RelativePath) || !seenMeshIds.Add(mesh.RelativePath))
            {
                continue;
            }

            var owner = FindOwningRoot(workspace, mesh.AbsolutePath);
            var sourceRoot = owner.HasValue ? owner.Value.Name : "workspace";
            var provenance = studioMode == WorkspaceMode.Repo
                ? AssetProvenance.Repo
                : (owner.HasValue && owner.Value.IsWritable ? AssetProvenance.Pack : AssetProvenance.Base);
            var detailPath = owner.HasValue
                ? ComputeRelativeFile(workspace, mesh.AbsolutePath)
                : mesh.RelativePath;

            meshCandidates.Add(new ReferenceCandidate(
                mesh.RelativePath,
                ReferenceKind.Mesh,
                provenance,
                ReferenceResolutionState.Resolved,
                sourceRoot,
                detailPath,
                ThumbnailPath: mesh.AbsolutePath));
        }

        for (var i = 0; i < workspaceImages.Count; i++)
        {
            var img = workspaceImages[i];
            if (string.IsNullOrWhiteSpace(img.RelativePath) || !seenTextureIds.Add(img.RelativePath))
            {
                continue;
            }

            var owner = FindOwningRoot(workspace, img.AbsolutePath);
            var sourceRoot = owner.HasValue ? owner.Value.Name : "workspace";
            var provenance = studioMode == WorkspaceMode.Repo
                ? AssetProvenance.Repo
                : (owner.HasValue && owner.Value.IsWritable ? AssetProvenance.Pack : AssetProvenance.Base);

            primaryTextureCandidates.Add(new ReferenceCandidate(
                img.RelativePath,
                ReferenceKind.Texture,
                provenance,
                ReferenceResolutionState.Resolved,
                sourceRoot,
                img.FolderHint,
                ThumbnailPath: img.AbsolutePath));
        }

        var naturalComparer = new NaturalComparer(StringComparison.OrdinalIgnoreCase);
        meshCandidates.Sort((left, right) =>
        {
            var cmp = naturalComparer.Compare(left.Value, right.Value);
            return cmp != 0
                ? cmp
                : StringComparer.OrdinalIgnoreCase.Compare(left.Value, right.Value);
        });
        primaryTextureCandidates.Sort((left, right) =>
        {
            var cmp = naturalComparer.Compare(left.Value, right.Value);
            return cmp != 0
                ? cmp
                : StringComparer.OrdinalIgnoreCase.Compare(left.Value, right.Value);
        });
        sidecarTextureCandidates.Sort((left, right) =>
        {
            var cmp = naturalComparer.Compare(left.Value, right.Value);
            return cmp != 0
                ? cmp
                : StringComparer.OrdinalIgnoreCase.Compare(left.Value, right.Value);
        });

        list.AddRange(meshCandidates);
        list.AddRange(primaryTextureCandidates);
        list.AddRange(sidecarTextureCandidates);

        var soundTagCandidates = new List<ReferenceCandidate>();
        var seenSoundTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var tagCandidates = new List<ReferenceCandidate>();
        var seenTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = workspace.TagEntries.Count - 1; i >= 0; i--)
        {
            var entry = workspace.TagEntries[i];
            if (string.IsNullOrWhiteSpace(entry.TagName))
            {
                continue;
            }

            var prov = studioMode == WorkspaceMode.Repo
                ? AssetProvenance.Repo
                : (entry.IsWritable ? AssetProvenance.Pack : AssetProvenance.Base);
            var relativeFile = ComputeRelativeFile(workspace, entry.FilePath);

            if (IsSoundTagEntry(in entry) && seenSoundTags.Add(entry.TagName))
            {
                var effectiveCount = workspace.GetEffectiveTagValues(entry.TagName, string.Empty).Count;
                var clipCount = effectiveCount > 0 ? effectiveCount : entry.Values.Count;
                soundTagCandidates.Add(new ReferenceCandidate(
                    entry.TagName,
                    ReferenceKind.SoundTag,
                    prov,
                    ReferenceResolutionState.Resolved,
                    entry.RootName,
                    $"{clipCount} clip(s) · {relativeFile}"));
            }

            if (string.IsNullOrWhiteSpace(entry.Locale) && seenTags.Add(entry.TagName))
            {
                var effectiveCount = workspace.GetEffectiveTagValues(entry.TagName, string.Empty).Count;
                var valueCount = effectiveCount > 0 ? effectiveCount : entry.Values.Count;
                tagCandidates.Add(new ReferenceCandidate(
                    entry.TagName,
                    ReferenceKind.Tag,
                    prov,
                    ReferenceResolutionState.Resolved,
                    entry.RootName,
                    $"{valueCount} value(s) · {relativeFile}"));
            }
        }

        soundTagCandidates.Sort(static (left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.Value, right.Value));
        tagCandidates.Sort(static (left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.Value, right.Value));
        list.AddRange(soundTagCandidates);
        list.AddRange(tagCandidates);

        var seenLoc = new HashSet<string>(StringComparer.Ordinal);
        for (var i = workspace.LocalizationEntries.Count - 1; i >= 0; i--)
        {
            var entry = workspace.LocalizationEntries[i];
            if (!seenLoc.Add(entry.Key))
            {
                continue;
            }

            var prov = studioMode == WorkspaceMode.Repo
                ? AssetProvenance.Repo
                : (entry.IsWritable ? AssetProvenance.Pack : AssetProvenance.Base);
            list.Add(new ReferenceCandidate(
                entry.Key,
                ReferenceKind.Localization,
                prov,
                ReferenceResolutionState.Resolved,
                entry.RootName,
                entry.Value));
        }

        return list;
    }

    private static List<DraftRecoveryEntry> ProjectDraftRecoveryEntries(
        ToolsWorkspace.WorkspaceIndex workspace,
        IReadOnlyList<ToolsStorage.DraftRecoveryEntry> drafts)
    {
        var list = new List<DraftRecoveryEntry>(drafts.Count);
        for (var i = 0; i < drafts.Count; i++)
        {
            var d = drafts[i];
            var rel = ComputeRelativeFile(workspace, d.DestinationPath);
            var savedAt = d.SavedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            var summary = d.HasExternalModification
                ? $"Unsaved draft ({d.DraftBytes.Length:N0} B) — disk file changed since draft was saved."
                : $"Unsaved draft ({d.DraftBytes.Length:N0} B) — disk file unchanged.";
            list.Add(new DraftRecoveryEntry(rel, savedAt, d.HasExternalModification, summary, d.DestinationPath));
        }

        return list;
    }

    private static List<InboundReferenceItem> ProjectInboundReferences(
        ToolsWorkspace.WorkspaceIndex workspace,
        string targetId)
    {
        var raw = workspace.GetInboundReferences(targetId);
        var list = new List<InboundReferenceItem>(raw.Count);
        var dedup = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < raw.Count; i++)
        {
            var r = raw[i];
            var key = $"{r.SourceAssetId}|{r.FieldName}|{r.SourceFilePath}";
            if (!dedup.Add(key))
            {
                continue;
            }

            list.Add(new InboundReferenceItem(
                r.SourceAssetId,
                ToStudioAssetKind(r.SourceKind),
                r.FieldName,
                ComputeRelativeFile(workspace, r.SourceFilePath)));
        }

        return list;
    }

    private static Dictionary<string, int> CountAssetEntriesPerFile(ToolsWorkspace.WorkspaceIndex workspace)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var asset in workspace.WinningAssets.Values)
        {
            var path = asset.Provenance.FilePath;
            counts[path] = counts.TryGetValue(path, out var existing) ? existing + 1 : 1;
        }

        return counts;
    }

    private static AssetProvenance MapProvenance(WorkspaceMode studioMode, in ToolsWorkspace.AssetProvenance prov)
    {
        if (studioMode == WorkspaceMode.Repo)
        {
            return AssetProvenance.Repo;
        }

        if (!prov.IsWritable)
        {
            return AssetProvenance.Base;
        }

        return prov.IsOverride ? AssetProvenance.Override : AssetProvenance.Pack;
    }

    private static AssetProvenance MapResourceProvenance(WorkspaceMode studioMode, in ToolsWorkspace.IndexedResource res)
    {
        if (studioMode == WorkspaceMode.Repo)
        {
            return AssetProvenance.Repo;
        }

        if (!res.IsWritable)
        {
            return AssetProvenance.Base;
        }

        return res.ShadowedRootIds.Count > 0 ? AssetProvenance.Override : AssetProvenance.Pack;
    }

    private static ToolsWorkspace.WorkspaceRoot? FindOwningRoot(ToolsWorkspace.WorkspaceIndex workspace, string filePath)
    {
        var full = Path.GetFullPath(filePath);
        for (var i = workspace.Roots.Count - 1; i >= 0; i--)
        {
            var root = workspace.Roots[i];
            var prefix = Path.GetFullPath(root.RootDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return root;
            }
        }

        return null;
    }

    private static string ResolveRootName(ToolsWorkspace.WorkspaceIndex workspace, string rootId, string filePath)
    {
        for (var i = 0; i < workspace.Roots.Count; i++)
        {
            if (string.Equals(workspace.Roots[i].Id, rootId, StringComparison.OrdinalIgnoreCase))
            {
                return workspace.Roots[i].Name;
            }
        }

        var owner = FindOwningRoot(workspace, filePath);
        return owner.HasValue ? owner.Value.Name : (string.IsNullOrWhiteSpace(rootId) ? "workspace" : rootId);
    }

    private static string ComputeRelativeFile(ToolsWorkspace.WorkspaceIndex workspace, string fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
        {
            return string.Empty;
        }

        var owner = FindOwningRoot(workspace, fullPath);
        if (owner.HasValue)
        {
            return Path.GetRelativePath(owner.Value.RootDirectory, fullPath).Replace('\\', '/');
        }

        return Path.GetRelativePath(workspace.RootPath, fullPath).Replace('\\', '/');
    }

    private static string ResolveAssetIdForFile(
        ToolsWorkspace.WorkspaceIndex workspace,
        string filePath,
        string relativeFile)
    {
        foreach (var asset in workspace.WinningAssets.Values)
        {
            if (string.Equals(asset.Provenance.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
            {
                return asset.Id;
            }
        }

        for (var i = 0; i < workspace.TagEntries.Count; i++)
        {
            if (string.Equals(workspace.TagEntries[i].FilePath, filePath, StringComparison.OrdinalIgnoreCase))
            {
                return workspace.TagEntries[i].TagName;
            }
        }

        return relativeFile.StartsWith("assets/", StringComparison.OrdinalIgnoreCase)
            ? relativeFile.Substring("assets/".Length)
            : Path.GetFileNameWithoutExtension(filePath);
    }

    private static string? MapFindingFieldKey(string field)
    {
        if (string.IsNullOrWhiteSpace(field))
        {
            return null;
        }

        var normalized = StripArrayIndexSuffix(field);
        return normalized switch
        {
            "Textures.Default" => "TextureDefault",
            "Textures.Top" => "TextureTop",
            "Textures.Bottom" => "TextureBottom",
            "Textures.Front" => "TextureFront",
            "Textures.Back" => "TextureBack",
            "Textures.Left" => "TextureLeft",
            "Textures.Right" => "TextureRight",
            "Sounds.Place" => "SoundPlace",
            "Sounds.Break" => "SoundBreak",
            "Placeable.ID" => "PlaceableID",
            "Placeable.Type" or "Items.Placeable.Type" => "PlaceableType",
            "Tool.Type" or "Items.Tool.Type" => "ToolType",
            "Tool.Target" or "Items.Tool.Target" => "ToolTarget",
            "ViewModel.Mesh" => "ViewModelMesh",
            "ViewModel.Material" => "ViewModelMaterial",
            "WorldModel.Mesh" => "WorldModelMesh",
            "WorldModel.Material" => "WorldModelMaterial",
            _ => normalized,
        };
    }

    private static string StripArrayIndexSuffix(string field)
    {
        var bracketIndex = field.IndexOf('[');
        return bracketIndex > 0 ? field.Substring(0, bracketIndex) : field;
    }

    private static List<string> DiscoverAvailableShaders(ToolsWorkspace.WorkspaceIndex workspace)
    {
        var discovered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var resource in workspace.WinningResources.Values)
        {
            if (resource.Kind == ToolsWorkspace.ResourceKind.Shader && !string.IsNullOrWhiteSpace(resource.Id))
            {
                discovered.Add(resource.Id.Replace('\\', '/'));
            }
        }

        for (var i = 0; i < workspace.Roots.Count; i++)
        {
            var root = workspace.Roots[i];
            if (!root.IsEnabled || string.IsNullOrWhiteSpace(root.AssetsDirectory))
            {
                continue;
            }

            ScanShaderDirectory(Path.Combine(root.AssetsDirectory, "shaders"), discovered);
        }

        if (workspace.Mode == ToolsWorkspace.WorkspaceMode.Repo)
        {
            ScanShaderDirectory(Path.Combine(workspace.RootPath, "Swordfish", "assets", "shaders"), discovered);
        }

        var sorted = new List<string>(discovered);
        sorted.Sort(StringComparer.OrdinalIgnoreCase);
        return sorted;
    }

    private static void ScanShaderDirectory(string shadersDir, HashSet<string> discovered)
    {
        if (string.IsNullOrWhiteSpace(shadersDir) || !Directory.Exists(shadersDir))
        {
            return;
        }

        try
        {
            foreach (var file in Directory.EnumerateFiles(shadersDir, "*.glsl", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(shadersDir, file).Replace('\\', '/');
                if (relative.EndsWith(".glsl", StringComparison.OrdinalIgnoreCase))
                {
                    relative = relative.Substring(0, relative.Length - 5);
                }

                if (!string.IsNullOrWhiteSpace(relative))
                {
                    discovered.Add(relative);
                }
            }
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Failed scanning shader directory '{shadersDir}': {ex.Message}");
        }
    }

    private static bool IsSoundTagEntry(in ToolsWorkspace.IndexedTagEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.Locale))
        {
            return false;
        }

        if (string.Equals(Path.GetFileName(entry.FilePath), "sounds.toml", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        for (var i = 0; i < entry.Values.Count; i++)
        {
            var value = entry.Values[i];
            if (value.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)
                || value.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase)
                || value.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)
                || value.EndsWith(".flac", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string? ExtractFirstSingleQuotedToken(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        var start = message.IndexOf('\'');
        if (start < 0 || start + 1 >= message.Length)
        {
            return null;
        }

        var end = message.IndexOf('\'', start + 1);
        if (end <= start + 1)
        {
            return null;
        }

        var token = message.Substring(start + 1, end - start - 1).Trim();
        return token.Length > 0 ? token : null;
    }

    private static bool IsFormattingFinding(in ToolsValidation.ValidationFinding finding)
    {
        if (string.Equals(finding.Code, "FMT001", StringComparison.Ordinal)
            || string.Equals(finding.Code, "FMT002", StringComparison.Ordinal))
        {
            return true;
        }

        if (finding.Severity != ToolsValidation.ValidationSeverity.Style)
        {
            return false;
        }

        if (finding.Code.StartsWith("FMT", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return finding.Message.Contains("format", StringComparison.OrdinalIgnoreCase)
            || finding.Message.Contains("align", StringComparison.OrdinalIgnoreCase)
            || finding.Message.Contains("indent", StringComparison.OrdinalIgnoreCase)
            || finding.Message.Contains("whitespace", StringComparison.OrdinalIgnoreCase)
            || finding.Message.Contains("line ending", StringComparison.OrdinalIgnoreCase)
            || finding.Message.Contains("BOM", StringComparison.Ordinal);
    }

    private static string? ResolveTextureDiskPath(ToolsWorkspace.WorkspaceIndex workspace, string texturePath)
    {
        if (string.IsNullOrWhiteSpace(texturePath))
        {
            return null;
        }

        var exact = workspace.GetResource(ToolsWorkspace.ResourceKind.Texture, texturePath);
        if (exact.Success && File.Exists(exact.Value.FilePath))
        {
            return exact.Value.FilePath;
        }

        var ci = workspace.FindResourceCaseInsensitive(ToolsWorkspace.ResourceKind.Texture, texturePath);
        return ci.Success && File.Exists(ci.Value.FilePath) ? ci.Value.FilePath : null;
    }

    private static string? ResolvePaintTextureDiskPath(
        ToolsWorkspace.WorkspaceIndex workspace,
        string layerName,
        bool connected)
    {
        if (string.IsNullOrWhiteSpace(layerName))
        {
            return null;
        }

        var trimmed = layerName.Trim().Replace('\\', '/');
        var candidates = new List<string>(4) { trimmed };
        if (trimmed.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            || trimmed.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase))
        {
            var withoutExt = Path.GetFileNameWithoutExtension(trimmed);
            if (!string.IsNullOrWhiteSpace(withoutExt))
            {
                candidates.Add(withoutExt);
            }
        }

        var stem = candidates[^1];
        if (!stem.EndsWith(".0", StringComparison.Ordinal))
        {
            candidates.Add($"{stem}.0");
        }
        else if (stem.Length > 2)
        {
            candidates.Add(stem.Substring(0, stem.Length - 2));
        }

        if (connected && !trimmed.EndsWith(".0", StringComparison.Ordinal) && !candidates.Contains($"{trimmed}.0", StringComparer.Ordinal))
        {
            candidates.Add($"{trimmed}.0");
        }

        for (var i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            if (workspace.WinningBlockAlbedoLayers.TryGetValue(candidate, out var exact)
                && File.Exists(exact.FilePath))
            {
                return exact.FilePath;
            }

            var ci = workspace.FindBlockAlbedoCaseInsensitive(candidate);
            if (ci.Success && File.Exists(ci.Value.FilePath))
            {
                return ci.Value.FilePath;
            }
        }

        // Fallback disk lookup inside enabled roots' assets/textures/block/ directories (highest priority first).
        for (var r = workspace.Roots.Count - 1; r >= 0; r--)
        {
            var root = workspace.Roots[r];
            if (!root.IsEnabled || string.IsNullOrWhiteSpace(root.AssetsDirectory))
            {
                continue;
            }

            var blockDir = Path.Combine(root.AssetsDirectory, "textures", "block");
            if (!Directory.Exists(blockDir))
            {
                continue;
            }

            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                var pngPath = Path.Combine(blockDir, candidate + ".png");
                if (File.Exists(pngPath))
                {
                    return Path.GetFullPath(pngPath);
                }

                var bmpPath = Path.Combine(blockDir, candidate + ".bmp");
                if (File.Exists(bmpPath))
                {
                    return Path.GetFullPath(bmpPath);
                }

                var directPath = Path.Combine(blockDir, candidate);
                if (File.Exists(directPath))
                {
                    return Path.GetFullPath(directPath);
                }
            }
        }

        for (var i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            var resolved = ResolveTextureDiskPath(workspace, $"block/{candidate}.png")
                ?? ResolveTextureDiskPath(workspace, $"block/{candidate}.bmp")
                ?? ResolveTextureDiskPath(workspace, candidate);
            if (resolved != null)
            {
                return resolved;
            }
        }

        return null;
    }

    private static (
        List<BlueprintPaletteItem> Palette,
        List<InboundReferenceItem> References,
        int BrickPaletteCount,
        int PaintPaletteCount,
        int LegacyRawIdCount,
        int SolidVoxelCount) ExtractBlueprintPalette(
        ToolsWorkspace.WorkspaceIndex workspace,
        string relativeId,
        in GameBlueprintData bp)
    {
        var brickCountsByIndex = new Dictionary<ushort, int>();
        var paintCountsByIndex = new Dictionary<ushort, int>();
        var solidVoxelCount = 0;

        if (bp.Chunks != null)
        {
            for (var c = 0; c < bp.Chunks.Length; c++)
            {
                var chunk = bp.Chunks[c].Chunk;
                var voxels = chunk.Voxels;
                if (voxels != null)
                {
                    for (var v = 0; v < voxels.Length; v++)
                    {
                        var brickIndex = voxels[v].ID;
                        if (brickIndex != 0)
                        {
                            solidVoxelCount++;
                            brickCountsByIndex[brickIndex] = brickCountsByIndex.TryGetValue(brickIndex, out var existing)
                                ? existing + 1
                                : 1;
                        }
                    }
                }

                var overrides = chunk.FaceOverrides;
                if (overrides != null)
                {
                    for (var o = 0; o < overrides.Length; o++)
                    {
                        var texPaint = overrides[o].TexturePaintId;
                        if (texPaint != 0)
                        {
                            paintCountsByIndex[texPaint] = paintCountsByIndex.TryGetValue(texPaint, out var existingTex)
                                ? existingTex + 1
                                : 1;
                        }

                        var colorPaint = overrides[o].ColorPaintId;
                        if (colorPaint != 0)
                        {
                            paintCountsByIndex[colorPaint] = paintCountsByIndex.TryGetValue(colorPaint, out var existingColor)
                                ? existingColor + 1
                                : 1;
                        }
                    }
                }
            }
        }

        var paletteItems = new List<BlueprintPaletteItem>();
        var references = new List<InboundReferenceItem>();
        var brickPaletteCount = 0;
        var paintPaletteCount = 0;
        var legacyRawIdCount = 0;

        if (bp.BrickPalette != null && bp.BrickPalette.Length > 1)
        {
            var brickCountsById = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var idx = 1; idx < bp.BrickPalette.Length; idx++)
            {
                var rawBrickId = bp.BrickPalette[idx];
                if (string.IsNullOrWhiteSpace(rawBrickId))
                {
                    continue;
                }

                var brickId = rawBrickId.Trim();
                var count = idx <= ushort.MaxValue && brickCountsByIndex.TryGetValue((ushort)idx, out var cCount)
                    ? cCount
                    : 0;
                brickCountsById[brickId] = brickCountsById.TryGetValue(brickId, out var prev)
                    ? prev + count
                    : count;
            }

            foreach (var kvp in brickCountsById
                .OrderByDescending(static e => e.Value)
                .ThenBy(static e => e.Key, StringComparer.OrdinalIgnoreCase))
            {
                var brickId = kvp.Key;
                var count = kvp.Value;
                var resolved = workspace.GetAsset(ToolsWorkspace.AssetKind.Brick, brickId);
                var isResolved = resolved.Success;
                var countLabel = $"{count:N0} voxel(s)";
                var summaryLabel = isResolved ? countLabel : $"{countLabel} · unresolved";
                var sourceFile = isResolved
                    ? ComputeRelativeFile(workspace, resolved.Value.Provenance.FilePath)
                    : "unresolved brick";

                paletteItems.Add(new BlueprintPaletteItem(brickId, AssetKind.Brick, count, isResolved, summaryLabel));
                references.Add(new InboundReferenceItem(
                    SourceAssetId: brickId,
                    SourceKind: AssetKind.Brick,
                    FieldPath: countLabel,
                    SourceFile: sourceFile));
                brickPaletteCount++;
            }
        }
        else if (bp.BrickCount > 0 || solidVoxelCount > 0)
        {
            legacyRawIdCount = brickCountsByIndex.Count;
            Trace.TraceWarning(
                $"Blueprint '{relativeId}' contains {solidVoxelCount} solid voxel(s) across {legacyRawIdCount} raw brick ID(s) with no BrickPalette (legacy unencoded .wbp).");

            foreach (var kvp in brickCountsByIndex
                .OrderByDescending(static e => e.Value)
                .ThenBy(static e => e.Key))
            {
                var rawLabel = $"#{kvp.Key}";
                var countLabel = $"{kvp.Value:N0} voxel(s)";
                paletteItems.Add(new BlueprintPaletteItem(
                    rawLabel,
                    AssetKind.Brick,
                    kvp.Value,
                    IsResolved: false,
                    SummaryLabel: $"legacy ID {rawLabel} · {countLabel}"));
                references.Add(new InboundReferenceItem(
                    SourceAssetId: rawLabel,
                    SourceKind: AssetKind.Brick,
                    FieldPath: $"{countLabel} (unencoded)",
                    SourceFile: "legacy unencoded blueprint"));
            }
        }

        if (bp.PaintPalette != null && bp.PaintPalette.Length > 1)
        {
            var paintCountsById = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var idx = 1; idx < bp.PaintPalette.Length; idx++)
            {
                var rawPaintId = bp.PaintPalette[idx];
                if (string.IsNullOrWhiteSpace(rawPaintId))
                {
                    continue;
                }

                var paintId = rawPaintId.Trim();
                var count = idx <= ushort.MaxValue && paintCountsByIndex.TryGetValue((ushort)idx, out var pCount)
                    ? pCount
                    : 0;
                paintCountsById[paintId] = paintCountsById.TryGetValue(paintId, out var prev)
                    ? prev + count
                    : count;
            }

            foreach (var kvp in paintCountsById
                .OrderByDescending(static e => e.Value)
                .ThenBy(static e => e.Key, StringComparer.OrdinalIgnoreCase))
            {
                var paintId = kvp.Key;
                var count = kvp.Value;
                var resolved = workspace.GetAsset(ToolsWorkspace.AssetKind.Paint, paintId);
                var isResolved = resolved.Success;
                var countLabel = $"{count:N0} face(s)";
                var summaryLabel = isResolved ? countLabel : $"{countLabel} · unresolved";
                var sourceFile = isResolved
                    ? ComputeRelativeFile(workspace, resolved.Value.Provenance.FilePath)
                    : "unresolved paint";

                paletteItems.Add(new BlueprintPaletteItem(paintId, AssetKind.Paint, count, isResolved, summaryLabel));
                references.Add(new InboundReferenceItem(
                    SourceAssetId: paintId,
                    SourceKind: AssetKind.Paint,
                    FieldPath: countLabel,
                    SourceFile: sourceFile));
                paintPaletteCount++;
            }
        }

        return (paletteItems, references, brickPaletteCount, paintPaletteCount, legacyRawIdCount, solidVoxelCount);
    }

    private static string FormatBlueprintBrickCountLabel(
        int brickCount,
        int brickPaletteCount,
        int paintPaletteCount,
        int legacyRawIdCount)
    {
        if (brickPaletteCount > 0 && paintPaletteCount > 0)
        {
            return $"{brickCount:N0} bricks · {brickPaletteCount} brick(s), {paintPaletteCount} paint(s)";
        }

        if (brickPaletteCount > 0)
        {
            var suffix = brickPaletteCount == 1 ? "entry" : "entries";
            return $"{brickCount:N0} bricks · {brickPaletteCount} palette {suffix}";
        }

        if (legacyRawIdCount > 0)
        {
            return $"{brickCount:N0} bricks · {legacyRawIdCount} legacy ID(s) (no palette)";
        }

        return $"{brickCount:N0} bricks";
    }

    private static void ScanWorkspaceAssetDirectory(
        in ToolsWorkspace.WorkspaceRoot root,
        string startDirectory,
        string preferredAssetsSubdir,
        Func<string, bool> includeFilePredicate,
        HashSet<string> scannedDirs,
        Dictionary<string, WorkspaceImageEntry> byRelativePath)
    {
        var preferredDir = string.IsNullOrWhiteSpace(root.AssetsDirectory)
            ? null
            : Path.Combine(root.AssetsDirectory, preferredAssetsSubdir);

        var pending = new Stack<string>();
        pending.Push(Path.GetFullPath(startDirectory));

        while (pending.Count > 0)
        {
            var currentDir = pending.Pop();
            if (!scannedDirs.Add(currentDir))
            {
                continue;
            }

            try
            {
                foreach (var file in Directory.EnumerateFiles(currentDir))
                {
                    var fileName = Path.GetFileName(file);
                    if (string.IsNullOrWhiteSpace(fileName)
                        || fileName.StartsWith('.')
                        || !includeFilePredicate(fileName))
                    {
                        continue;
                    }

                    var fullFile = Path.GetFullPath(file);
                    string relativePath;
                    string folderHint;

                    if (TryMakeRelativeUnder(preferredDir, fullFile, out var relToPreferred))
                    {
                        relativePath = relToPreferred;
                        folderHint = ExtractFolderHint(relToPreferred, preferredAssetsSubdir);
                    }
                    else if (TryMakeRelativeUnder(root.AssetsDirectory, fullFile, out var relToAssets))
                    {
                        relativePath = relToAssets;
                        folderHint = ExtractFolderHint(relToAssets, "assets");
                    }
                    else if (TryMakeRelativeUnder(root.RootDirectory, fullFile, out var relToRoot))
                    {
                        relativePath = relToRoot;
                        folderHint = ExtractFolderHint(relToRoot, root.Name);
                    }
                    else
                    {
                        relativePath = fileName;
                        folderHint = root.Name;
                    }

                    byRelativePath[relativePath] = new WorkspaceImageEntry(relativePath, fileName, fullFile, folderHint);
                }
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"Failed enumerating files in '{currentDir}': {ex.Message}");
            }

            try
            {
                foreach (var subDir in Directory.EnumerateDirectories(currentDir))
                {
                    var dirName = Path.GetFileName(subDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                    if (string.IsNullOrWhiteSpace(dirName)
                        || dirName.StartsWith('.')
                        || IGNORED_SCAN_DIRECTORIES.Contains(dirName))
                    {
                        continue;
                    }

                    pending.Push(Path.GetFullPath(subDir));
                }
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"Failed enumerating subdirectories in '{currentDir}': {ex.Message}");
            }
        }
    }

    private static int GetWorkspaceImageSortRank(string relativePath)
    {
        if (relativePath.StartsWith("characters/", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (relativePath.StartsWith("items/", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return 2;
    }

    private static bool HasImageExtension(string fileName)
    {
        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(ext))
        {
            return false;
        }

        for (var i = 0; i < IMAGE_EXTENSIONS.Length; i++)
        {
            if (string.Equals(ext, IMAGE_EXTENSIONS[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryMakeRelativeUnder(string? baseDir, string fullFilePath, out string relative)
    {
        relative = string.Empty;
        if (string.IsNullOrWhiteSpace(baseDir))
        {
            return false;
        }

        var normalizedBase = Path.GetFullPath(baseDir)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var prefix = normalizedBase + Path.DirectorySeparatorChar;
        if (!fullFilePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        relative = Path.GetRelativePath(normalizedBase, fullFilePath).Replace('\\', '/');
        return !string.IsNullOrWhiteSpace(relative) && !relative.StartsWith("..", StringComparison.Ordinal);
    }

    private static string ExtractFolderHint(string relativePath, string fallback)
    {
        var lastSlash = relativePath.LastIndexOf('/');
        return lastSlash > 0 ? relativePath.Substring(0, lastSlash) : fallback;
    }

    private static string ResolvePackId(
        ToolsPacks.PackMetadata? packMeta,
        Result<ToolsWorkspace.WorkspaceRoot> primaryWritable,
        string folderName)
    {
        if (packMeta.HasValue && !string.IsNullOrWhiteSpace(packMeta.Value.Id))
        {
            return packMeta.Value.Id.Trim();
        }

        if (primaryWritable.Success && primaryWritable.Value.Manifest.HasValue
            && !string.IsNullOrWhiteSpace(primaryWritable.Value.Manifest.Value.Id))
        {
            return primaryWritable.Value.Manifest.Value.Id.Trim();
        }

        if (primaryWritable.Success && !string.IsNullOrWhiteSpace(primaryWritable.Value.Id))
        {
            return primaryWritable.Value.Id.Trim();
        }

        return !string.IsNullOrWhiteSpace(folderName) ? folderName : "pack";
    }

    private static string ResolvePackNamespace(ToolsPacks.PackMetadata? packMeta, string packId)
    {
        if (packMeta.HasValue && !string.IsNullOrWhiteSpace(packMeta.Value.Namespace))
        {
            return packMeta.Value.Namespace.Trim().TrimEnd(':').Trim();
        }

        var fallback = packId.Trim().TrimEnd(':').Trim();
        return fallback.Length > 0 ? fallback : "pack";
    }

    private static string ResolveEffectiveWorkspaceDisplayName(
        ToolsWorkspace.WorkspaceIndex workspace,
        WorkspaceMode studioMode,
        string packId,
        ToolsPacks.PackMetadata? packMeta,
        Result<ToolsWorkspace.WorkspaceRoot> primaryWritable,
        string? explicitDisplayName)
    {
        if (!string.IsNullOrWhiteSpace(explicitDisplayName))
        {
            var trimmed = explicitDisplayName.Trim();
            if (studioMode != WorkspaceMode.Pack
                || !string.Equals(trimmed, "Wayward Beyond", StringComparison.OrdinalIgnoreCase)
                || IsPackLiterallyNamedWaywardBeyond(workspace.RootPath, packMeta, primaryWritable))
            {
                return trimmed;
            }
        }

        return ResolveWorkspaceDisplayName(workspace, studioMode, packId, packMeta, primaryWritable);
    }

    private static bool IsPackLiterallyNamedWaywardBeyond(
        string rootPath,
        ToolsPacks.PackMetadata? packMeta,
        Result<ToolsWorkspace.WorkspaceRoot> primaryWritable)
    {
        var folder = GetDirectoryDisplayName(rootPath);
        if (string.Equals(folder, "Wayward Beyond", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (packMeta.HasValue && string.Equals(packMeta.Value.Id?.Trim(), "Wayward Beyond", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (primaryWritable.Success && primaryWritable.Value.Manifest.HasValue)
        {
            var manifest = primaryWritable.Value.Manifest.Value;
            if (string.Equals(manifest.Name?.Trim(), "Wayward Beyond", StringComparison.OrdinalIgnoreCase)
                || string.Equals(manifest.Id?.Trim(), "Wayward Beyond", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string GetDirectoryDisplayName(string? directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return string.Empty;
        }

        return Path.GetFileName(directoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    }

    private static string ResolveWorkspaceDisplayName(
        ToolsWorkspace.WorkspaceIndex workspace,
        WorkspaceMode studioMode,
        string packId,
        ToolsPacks.PackMetadata? packMeta,
        Result<ToolsWorkspace.WorkspaceRoot> primaryWritable)
    {
        var folder = GetDirectoryDisplayName(workspace.RootPath);

        if (studioMode == WorkspaceMode.Repo)
        {
            if (File.Exists(Path.Combine(workspace.RootPath, "Swordfish.sln"))
                && Directory.Exists(Path.Combine(workspace.RootPath, "WaywardBeyond.Bricks")))
            {
                return "Wayward Beyond";
            }

            return !string.IsNullOrWhiteSpace(folder) ? folder : "workspace";
        }

        if (primaryWritable.Success && primaryWritable.Value.Manifest.HasValue
            && !string.IsNullOrWhiteSpace(primaryWritable.Value.Manifest.Value.Name))
        {
            return primaryWritable.Value.Manifest.Value.Name.Trim();
        }

        if (packMeta.HasValue && !string.IsNullOrWhiteSpace(packMeta.Value.Id))
        {
            return packMeta.Value.Id.Trim();
        }

        if (primaryWritable.Success && primaryWritable.Value.Manifest.HasValue
            && !string.IsNullOrWhiteSpace(primaryWritable.Value.Manifest.Value.Id))
        {
            return primaryWritable.Value.Manifest.Value.Id.Trim();
        }

        if (!string.IsNullOrWhiteSpace(folder))
        {
            return folder;
        }

        return !string.IsNullOrWhiteSpace(packId) ? packId : "pack";
    }

    private static AssetKind InferAssetKindFromPath(string relativeFile)
    {
        var lower = relativeFile.ToLowerInvariant();
        if (lower.Contains("/items/"))
        {
            return AssetKind.Item;
        }

        if (lower.Contains("/paints/"))
        {
            return AssetKind.Paint;
        }

        if (lower.Contains("/skills/"))
        {
            return AssetKind.Skill;
        }

        if (lower.Contains("/bodies/"))
        {
            return AssetKind.Body;
        }

        if (lower.Contains("/materials/"))
        {
            return AssetKind.Material;
        }

        if (lower.Contains("/tags/"))
        {
            return AssetKind.Tag;
        }

        if (lower.Contains("/lang/"))
        {
            return AssetKind.Localization;
        }

        return AssetKind.Brick;
    }

    private static string InferGroupTitle(AssetKind kind)
    {
        return kind switch
        {
            AssetKind.Item => "Items",
            AssetKind.Paint => "Paints",
            AssetKind.Skill => "Skills",
            AssetKind.Body => "Bodies",
            AssetKind.Material => "Materials",
            AssetKind.Tag => "Tags",
            AssetKind.Localization => "Localization",
            AssetKind.Blueprint => "Blueprints",
            _ => "Bricks",
        };
    }

    private static string JoinNullableArray(string?[]? items)
    {
        if (items == null || items.Length == 0)
        {
            return string.Empty;
        }

        var nonEmpty = new List<string>(items.Length);
        for (var i = 0; i < items.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(items[i]))
            {
                nonEmpty.Add(items[i]!);
            }
        }

        return string.Join(", ", nonEmpty);
    }

    private static string FirstNonEmpty(string?[] items)
    {
        for (var i = 0; i < items.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(items[i]))
            {
                return items[i]!;
            }
        }

        return string.Empty;
    }

    private static string NormalizeHexOrDefault(string? hex, string fallback)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return fallback;
        }

        var trimmed = hex.Trim();
        return trimmed.StartsWith('#') ? trimmed : "#" + trimmed;
    }

    private static string FormatFloat3(float x, float y, float z)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{x:0.####}, {y:0.####}, {z:0.####}");
    }

    private static string FormatUnixMs(long unixMs)
    {
        if (unixMs <= 0)
        {
            return "Unknown";
        }

        return DateTimeOffset.FromUnixTimeMilliseconds(unixMs)
            .ToUniversalTime()
            .ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);
    }

    private static bool TryGetCaseInsensitive<T>(
        Dictionary<string, T> dict,
        string key,
        out T? value)
    {
        if (dict.TryGetValue(key, out value))
        {
            return true;
        }

        foreach (var kvp in dict)
        {
            if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                value = kvp.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}

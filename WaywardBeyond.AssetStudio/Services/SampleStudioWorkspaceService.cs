using System.Globalization;
using System.Runtime.InteropServices;
using Swordfish.Library.Util;
using WaywardBeyond.AssetStudio.Models;
using ToolsEditing = WaywardBeyond.AssetTools.Editing;
using ToolsSettings = WaywardBeyond.AssetTools.Settings;
using ToolsValidation = WaywardBeyond.AssetTools.Validation;
using ToolsWorkspace = WaywardBeyond.AssetTools.Workspace;

namespace WaywardBeyond.AssetStudio.Services;

/// <summary>
///     Provides realistic Wayward Beyond workspace fixtures for design-time and headless studio runs.
/// </summary>
public sealed class SampleStudioWorkspaceService : IStudioWorkspaceService
{
    private static readonly string[] SAMPLE_SHADERS =
    [
        "lighted",
        "lightedArray",
        "textured",
        "voxelGhost",
        "brick_icon",
        "laser_beam",
    ];

    private readonly Dictionary<string, string> _workspaceNamespaces = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<RecentWorkspaceEntry> _recentWorkspaces =
    [
        new RecentWorkspaceEntry(
            Name: "my_colony",
            Mode: WorkspaceMode.Pack,
            RootPath: "~/packs/my_colony",
            LastOpenedLabel: "Today, 14:12",
            AssetCount: 14,
            HasPackMetadata: true),
        new RecentWorkspaceEntry(
            Name: "swordfish-engine (Repo)",
            Mode: WorkspaceMode.Repo,
            RootPath: "~/workspace/swordfish-engine",
            LastOpenedLabel: "Yesterday, 19:45",
            AssetCount: 148,
            HasPackMetadata: false),
        new RecentWorkspaceEntry(
            Name: "orbital_outpost",
            Mode: WorkspaceMode.Pack,
            RootPath: "~/packs/orbital_outpost",
            LastOpenedLabel: "Nov 22, 2026",
            AssetCount: 38,
            HasPackMetadata: true),
        new RecentWorkspaceEntry(
            Name: "industrial_paints",
            Mode: WorkspaceMode.Pack,
            RootPath: "~/packs/industrial_paints",
            LastOpenedLabel: "Nov 18, 2026",
            AssetCount: 32,
            HasPackMetadata: false),
    ];

    private WorkspaceMode _lastOpenedMode = WorkspaceMode.Pack;
    private string? _lastOpenedRootPath;
    private string? _lastOpenedDisplayName;
    private List<StudioAssetSeed>? _sessionAssets;
    private List<ValidationFinding>? _sessionFindings;
    private string _sessionRootLabel = "my_colony";

    private StudioSettingsState _settings = new(
        IsDarkTheme: true,
        CultureOverride: "System Default",
        GlPreference: "Auto",
        LogLevel: "Information",
        IsPortableMode: false,
        SettingsPath: "~/.config/WaywardBeyond.AssetStudio/settings.toml",
        Theme: ToolsSettings.ToolTheme.Dark,
        UiScale: 1.0,
        DefaultNamespace: string.Empty);

    /// <inheritdoc/>
    public bool UsesRealBackend => false;

    /// <inheritdoc/>
    public IReadOnlyList<RecentWorkspaceEntry> GetRecentWorkspaces()
    {
        return [.. _recentWorkspaces];
    }

    /// <inheritdoc/>
    public StudioOperationResult RemoveRecentWorkspace(string rootPath, bool deleteFromDisk)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            return new StudioOperationResult(false, "Cannot remove a recent workspace with an empty path.");
        }

        var trimmed = rootPath.Trim();
        var removedCount = _recentWorkspaces.RemoveAll(entry =>
            string.Equals(entry.RootPath, trimmed, StringComparison.OrdinalIgnoreCase));
        if (removedCount == 0)
        {
            return new StudioOperationResult(false, $"Recent workspace '{trimmed}' was not found.");
        }

        _workspaceNamespaces.Remove(trimmed);
        var suffix = deleteFromDisk ? " and deleted it from disk (sample mode)" : string.Empty;
        return new StudioOperationResult(true, $"Removed '{trimmed}' from recent workspaces{suffix}.");
    }

    /// <inheritdoc/>
    public StudioWorkspaceSnapshot OpenWorkspace(WorkspaceMode mode, string rootPath, string? displayName = null)
    {
        _lastOpenedMode = mode;
        _lastOpenedRootPath = rootPath;
        _lastOpenedDisplayName = displayName;

        var workspaceName = string.IsNullOrWhiteSpace(displayName)
            ? (mode == WorkspaceMode.Repo ? "swordfish-engine" : "my_colony")
            : displayName;
        var rootLabel = mode == WorkspaceMode.Repo ? "repo" : workspaceName;
        var packNamespace = mode == WorkspaceMode.Pack ? "colony" : "wb";
        var defaultNamespace = GetWorkspaceDefaultNamespace(rootPath);
        if (string.IsNullOrWhiteSpace(defaultNamespace))
        {
            defaultNamespace = packNamespace;
        }

        var assets = BuildSampleAssets(mode, rootLabel);
        var findings = BuildInitialFindings(rootLabel);
        _sessionRootLabel = rootLabel;
        _sessionAssets = assets;
        _sessionFindings = findings;
        return BuildSessionSnapshot(mode, rootPath, workspaceName, rootLabel, packNamespace, defaultNamespace, assets, findings);
    }

    private StudioWorkspaceSnapshot BuildSessionSnapshot(
        WorkspaceMode mode,
        string rootPath,
        string workspaceName,
        string rootLabel,
        string packNamespace,
        string defaultNamespace,
        List<StudioAssetSeed> assets,
        List<ValidationFinding> findings)
    {
        var candidates = BuildCandidates(rootLabel);
        var pendingDrafts = BuildPendingDrafts();
        var workspaceImages = BuildSampleWorkspaceImages(rootPath);

        return new StudioWorkspaceSnapshot
        {
            WorkspaceName = workspaceName,
            Mode = mode,
            RootPath = rootPath,
            ReferenceRootPath = mode == WorkspaceMode.Pack ? "/games/WaywardBeyond/install" : null,
            ContentVersion = 1,
            PackId = mode == WorkspaceMode.Pack ? "my_colony" : "wayward_beyond",
            PackNamespace = packNamespace,
            DefaultNamespace = defaultNamespace,
            AvailableShaders = SAMPLE_SHADERS,
            WorkspaceImages = workspaceImages,
            IsUninitializedFolder = false,
            Assets = assets,
            Findings = findings,
            Candidates = candidates,
            PendingDrafts = pendingDrafts,
        };
    }

    /// <inheritdoc/>
    public StudioSettingsState GetSettings()
    {
        var ns = _lastOpenedRootPath != null && _workspaceNamespaces.TryGetValue(_lastOpenedRootPath, out var savedNs)
            ? savedNs
            : _settings.DefaultNamespace;
        return _settings with { DefaultNamespace = ns };
    }

    /// <inheritdoc/>
    public StudioOperationResult SaveSettings(StudioSettingsState settings)
    {
        var resolvedTheme = settings.Theme == ToolsSettings.ToolTheme.System && settings.IsDarkTheme
            ? ToolsSettings.ToolTheme.Dark
            : settings.Theme;
        _settings = settings with
        {
            Theme = resolvedTheme,
            IsDarkTheme = resolvedTheme == ToolsSettings.ToolTheme.Dark,
            UiScale = ToolsSettings.ToolSettings.ClampUiScale(settings.UiScale),
        };

        if (!string.IsNullOrWhiteSpace(_lastOpenedRootPath))
        {
            SetWorkspaceDefaultNamespace(_lastOpenedRootPath, settings.DefaultNamespace);
        }

        return new StudioOperationResult(true, "Settings saved.", settings.SettingsPath);
    }

    /// <inheritdoc/>
    public IReadOnlyList<RenameAffectedSpan> PreviewRename(string currentId, string newId, AssetKind kind)
    {
        var sanitized = string.IsNullOrWhiteSpace(newId) ? currentId : newId.Trim();
        if (kind == AssetKind.Brick)
        {
            return
            [
                new RenameAffectedSpan("assets/bricks/thruster.toml", 2, "[[Bricks]].ID", $"ID = \"{currentId}\"", $"ID = \"{sanitized}\""),
                new RenameAffectedSpan("assets/items/thruster.toml", 8, "[Items.Placeable].ID", $"ID = \"{currentId}\"", $"ID = \"{sanitized}\""),
                new RenameAffectedSpan("assets/lang/tags/bricks.toml", 29, "[Tags].\"brick.paintable\"", $"\"{currentId}\"", $"\"{sanitized}\""),
                new RenameAffectedSpan("assets/skills/building.toml", 8, "[Skills.Sources.Place]", $"\"{currentId}\" = 5", $"\"{sanitized}\" = 5"),
            ];
        }

        return
        [
            new RenameAffectedSpan("assets/items/thruster.toml", 2, "[[Items]].ID", $"ID = \"{currentId}\"", $"ID = \"{sanitized}\""),
            new RenameAffectedSpan("assets/lang/tags/items.toml", 11, "[Tags].\"item.placeable\"", $"\"{currentId}\"", $"\"{sanitized}\""),
        ];
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> GetExistingFilesForKind(AssetKind kind)
    {
        return kind switch
        {
            AssetKind.Brick => ["assets/bricks/thruster.toml", "assets/bricks/panel.toml", "assets/bricks/glass.toml"],
            AssetKind.Item => ["assets/items/thruster.toml", "assets/items/drill.toml"],
            AssetKind.Paint => ["assets/paints/colors.toml", "assets/paints/textures.toml"],
            AssetKind.Recipe => ["assets/recipes/crafting.toml"],
            AssetKind.SlotGroup => ["assets/inventory/slot_groups.toml"],
            AssetKind.Skill => ["assets/skills/building.toml", "assets/skills/mining.toml"],
            AssetKind.Body => ["assets/bodies/human_m.toml", "assets/bodies/bot.toml"],
            AssetKind.Material => ["assets/materials/bricks/glass.toml", "assets/materials/bricks/thruster.toml"],
            AssetKind.Tag => ["assets/lang/tags/bricks.toml", "assets/lang/tags/items.toml", "assets/lang/tags/paints.toml"],
            AssetKind.Localization => ["assets/lang/en/item.toml", "assets/lang/en/brick.toml", "assets/lang/en/paint.toml", "assets/lang/en/skills.toml"],
            AssetKind.Blueprint => ["assets/blueprints/ships/scout_mk1.wbp"],
            _ => ["assets/custom.toml"],
        };
    }

    /// <inheritdoc/>
    public string GetDefaultLocalizationFile(AssetKind kind)
    {
        return kind switch
        {
            AssetKind.Item => "assets/lang/en/item.toml",
            AssetKind.Brick => "assets/lang/en/brick.toml",
            AssetKind.Paint => "assets/lang/en/paint.toml",
            AssetKind.Recipe => "assets/lang/en/recipes.toml",
            AssetKind.Skill => "assets/lang/en/skills.toml",
            AssetKind.Body => "assets/lang/en/body.toml",
            _ => "assets/lang/en/ui.toml",
        };
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> GetLocalizationFiles()
    {
        return
        [
            "assets/lang/en/item.toml",
            "assets/lang/en/brick.toml",
            "assets/lang/en/paint.toml",
            "assets/lang/en/skills.toml",
            "assets/lang/en/body.toml",
            "assets/lang/en/ui.toml",
        ];
    }

    /// <inheritdoc/>
    public string BuildStagedPackToml(string packId, string packNamespace, int contentVersion)
    {
        return $"""
            ID             = "{packId}"
            Namespace      = "{packNamespace}"
            ContentVersion = {contentVersion}
            ToolVersion    = "0.1.0-alpha"
            CreatedAt      = "2026-11-26T00:30:00Z"
            """;
    }

    /// <inheritdoc/>
    public StudioOperationResult ExportPack(ExportPackRequest request)
    {
        var isTarGz = !string.IsNullOrWhiteSpace(request.ArchiveFormat)
            && request.ArchiveFormat.Contains("tar", StringComparison.OrdinalIgnoreCase);
        var extension = isTarGz ? ".tar.gz" : ".zip";
        var artifact = $"{request.PackId}-{request.ContentVersion}{extension}";
        return new StudioOperationResult(true, $"Staged and exported {artifact}", artifact);
    }

    /// <inheritdoc/>
    public AboutDiagnosticsInfo GetDiagnosticsInfo(WorkspaceMode mode, string rootPath, string? referenceRoot, string glVendorRenderer)
    {
        var roots = referenceRoot != null
            ? $"Writable: {rootPath}\nReference: {referenceRoot}"
            : $"Writable: {rootPath}";
        return new AboutDiagnosticsInfo(
            ToolVersion: "0.1.0-alpha",
            ContentVersion: 1,
            WorkspaceModeLabel: mode == WorkspaceMode.Pack ? "Pack Mode" : "Repository Mode",
            RootsSummary: roots,
            OsArchitecture: $"{RuntimeInformation.OSDescription.Trim()} ({RuntimeInformation.OSArchitecture})",
            GlVendorRenderer: glVendorRenderer,
            AvaloniaVersion: "11.3.23",
            LogPath: "~/.local/share/WaywardBeyond.AssetStudio/logs/studio-2026-11-26.log");
    }

    /// <inheritdoc/>
    public StudioOperationResult CopyDiagnosticsZip(AboutDiagnosticsInfo info)
    {
        return new StudioOperationResult(
            true,
            "Diagnostics archive written (about.json, log tail, validation summary; content excluded).",
            "~/Desktop/wayward-asset-studio-diagnostics.zip");
    }

    /// <inheritdoc/>
    public StudioWorkspaceSnapshot? TryOpenAuto(string rootPath, string? referenceInstallPath = null)
    {
        return OpenWorkspace(WorkspaceMode.Pack, rootPath);
    }

    /// <inheritdoc/>
    public StudioOperationResult CreatePackWorkspace(
        string directory,
        string packId,
        string packNamespace,
        string displayName,
        string author)
    {
        return new StudioOperationResult(true, $"Scaffolded pack '{packId}' in {directory}.", directory);
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> LocateGameInstalls() => ["/games/WaywardBeyond/install"];

    /// <inheritdoc/>
    public WorkspaceSaveResult SaveDocuments(IReadOnlyList<DocumentSavePayload> dirtyDocuments)
    {
        var count = dirtyDocuments.Count;
        return new WorkspaceSaveResult(
            true,
            count,
            count > 0
                ? $"Saved {count} draft file(s) atomically (fsync + rename verified)."
                : "All documents are already up to date.");
    }

    /// <inheritdoc/>
    public StudioOperationResult SaveDraft(DocumentSavePayload payload)
    {
        return new StudioOperationResult(true, $"Draft synced for {payload.RelativeFile}.");
    }

    /// <inheritdoc/>
    public string? PreviewEditedToml(DocumentSavePayload payload) => null;

    /// <inheritdoc/>
    public StudioOperationResult RecoverDraft(DraftRecoveryEntry entry)
    {
        return new StudioOperationResult(true, $"Recovered draft for {entry.RelativeFile}.");
    }

    /// <inheritdoc/>
    public StudioOperationResult DiscardDraft(DraftRecoveryEntry entry)
    {
        return new StudioOperationResult(true, $"Discarded draft for {entry.RelativeFile}.");
    }

    /// <inheritdoc/>
    public StudioOperationResult DiscardDraft(string? absoluteFilePath)
    {
        return new StudioOperationResult(true, "Discarded draft.");
    }

    /// <inheritdoc/>
    public StudioOperationResult DiscardAllDrafts()
    {
        return new StudioOperationResult(true, "Discarded all unsaved drafts.");
    }

    /// <inheritdoc/>
    public StudioOperationResult DeleteAsset(
        AssetKind kind,
        string assetId,
        string? absoluteFilePath,
        int tableIndex,
        int entryIndex)
    {
        return new StudioOperationResult(true, $"Deleted {kind} '{assetId}'.", absoluteFilePath);
    }

    /// <inheritdoc/>
    public StudioWorkspaceSnapshot? ReloadCurrentWorkspaceFromDisk()
    {
        if (string.IsNullOrWhiteSpace(_lastOpenedRootPath))
        {
            return null;
        }

        if (_sessionAssets != null && _sessionFindings != null)
        {
            var workspaceName = string.IsNullOrWhiteSpace(_lastOpenedDisplayName)
                ? (_lastOpenedMode == WorkspaceMode.Repo ? "swordfish-engine" : "my_colony")
                : _lastOpenedDisplayName;
            var packNamespace = _lastOpenedMode == WorkspaceMode.Pack ? "colony" : "wb";
            var defaultNamespace = GetWorkspaceDefaultNamespace(_lastOpenedRootPath);
            if (string.IsNullOrWhiteSpace(defaultNamespace))
            {
                defaultNamespace = packNamespace;
            }

            return BuildSessionSnapshot(
                _lastOpenedMode,
                _lastOpenedRootPath,
                workspaceName,
                _sessionRootLabel,
                packNamespace,
                defaultNamespace,
                _sessionAssets,
                _sessionFindings);
        }

        return OpenWorkspace(_lastOpenedMode, _lastOpenedRootPath, _lastOpenedDisplayName);
    }

    /// <inheritdoc/>
    public string GetWorkspaceDefaultNamespace(string? rootPath)
    {
        var key = !string.IsNullOrWhiteSpace(rootPath) ? rootPath : _lastOpenedRootPath;
        if (!string.IsNullOrWhiteSpace(key) && _workspaceNamespaces.TryGetValue(key, out var ns))
        {
            return ns;
        }

        return _lastOpenedMode == WorkspaceMode.Repo ? "wb" : "colony";
    }

    /// <inheritdoc/>
    public void SetWorkspaceDefaultNamespace(string? rootPath, string defaultNamespace)
    {
        var key = !string.IsNullOrWhiteSpace(rootPath) ? rootPath : _lastOpenedRootPath;
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        var cleaned = (defaultNamespace ?? string.Empty).Trim().TrimEnd(':').Trim();
        if (cleaned.Length == 0)
        {
            _workspaceNamespaces.Remove(key);
        }
        else
        {
            _workspaceNamespaces[key] = cleaned;
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> GetWatchedDirectories() => [];

    /// <inheritdoc/>
    public bool WasRecentlySavedByStudio(string filePath) => false;

    /// <inheritdoc/>
    public StudioValidationSyncResult RevalidateWorkspace()
    {
        return new StudioValidationSyncResult(false, [], string.Empty);
    }

    /// <inheritdoc/>
    public IReadOnlyList<InboundReferenceItem> GetInboundReferences(AssetKind kind, string assetId)
    {
        return [];
    }

    /// <inheritdoc/>
    public StudioOperationResult FixCaseIssues()
    {
        return new StudioOperationResult(true, "Applied sample case fix.");
    }

    /// <inheritdoc/>
    public StudioOperationResult FormatDocument(string filePath)
    {
        return new StudioOperationResult(true, $"Formatted {filePath} with house alignment.");
    }

    /// <inheritdoc/>
    public Result<bool> ApplyValidationFix(ValidationFinding finding)
    {
        if (_sessionAssets == null || _sessionFindings == null)
        {
            return Result<bool>.FromFailure("No sample workspace is open.");
        }

        if (!finding.HasQuickFix)
        {
            return Result<bool>.FromSuccess(false);
        }

        var applied = ApplySampleFindingToSessionAssets(finding);
        _sessionFindings.RemoveAll(f => string.Equals(f.Id, finding.Id, StringComparison.Ordinal)
            && string.Equals(f.RelativeFile, finding.RelativeFile, StringComparison.OrdinalIgnoreCase));
        return Result<bool>.FromSuccess(applied);
    }

    /// <inheritdoc/>
    public Result<ToolsValidation.BulkFixResult> FixAllSafeIssues()
    {
        if (_sessionAssets == null || _sessionFindings == null)
        {
            return Result<ToolsValidation.BulkFixResult>.FromFailure("No sample workspace is open.");
        }

        var appliedCount = 0;
        var modifiedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = _sessionFindings.Count - 1; i >= 0; i--)
        {
            var finding = _sessionFindings[i];
            if (!finding.IsSafeBulkFix || !finding.HasQuickFix)
            {
                continue;
            }

            if (ApplySampleFindingToSessionAssets(finding))
            {
                appliedCount++;
                if (!string.IsNullOrWhiteSpace(finding.RelativeFile))
                {
                    modifiedFiles.Add(finding.RelativeFile);
                }

                _sessionFindings.RemoveAt(i);
            }
        }

        return Result<ToolsValidation.BulkFixResult>.FromSuccess(new ToolsValidation.BulkFixResult(
            appliedCount,
            modifiedFiles.Count,
            SkippedReadOnlyCount: 0,
            ModifiedFiles: [.. modifiedFiles],
            SkippedCount: 0));
    }

    /// <inheritdoc/>
    public string FormatValidationPlainText(IReadOnlyList<ValidationFinding> fallbackFindings)
    {
        var lines = new List<string>(fallbackFindings.Count);
        for (var i = 0; i < fallbackFindings.Count; i++)
        {
            var f = fallbackFindings[i];
            lines.Add($"[{f.Severity}] {f.RootName}/{f.RelativeFile}:{f.LineNumber} {f.Message}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    /// <inheritdoc/>
    public StudioOperationResult ApplyRenameToDrafts(string currentId, string newId, AssetKind kind)
    {
        return new StudioOperationResult(true, $"Renamed '{currentId}' -> '{newId}' in workspace drafts.");
    }

    /// <inheritdoc/>
    public StudioOperationResult CreateAssetDocument(NewAssetRequest request)
    {
        return new StudioOperationResult(true, $"Created {request.Kind} '{request.AssetId}' in {request.TargetRelativeFile}.");
    }

    /// <inheritdoc/>
    public StudioOperationResult CreateLocalizationKey(string keyName, string targetRelativeFile, string initialText)
    {
        return new StudioOperationResult(true, $"Created key '{keyName}' in {targetRelativeFile}.");
    }

    /// <inheritdoc/>
    public StudioOperationResult ExecuteBlueprintAction(string action, string relativeId, string? argument = null)
    {
        return new StudioOperationResult(true, $"Executed blueprint action '{action}' on '{relativeId}'.");
    }

    /// <inheritdoc/>
    public Result<ToolsEditing.AssetClipboardPayload> CaptureAssetClipboard(
        AssetKind kind,
        string assetId,
        string? absoluteFilePath,
        int tableIndex)
    {
        var source = FindSampleAsset(kind, assetId, absoluteFilePath);
        if (source == null)
        {
            return Result<ToolsEditing.AssetClipboardPayload>.FromFailure($"Could not find {kind} '{assetId}'.");
        }

        var toolsKind = AssetSeedProjector.ToToolsAssetKind(source.Kind);
        var sourcePath = !string.IsNullOrWhiteSpace(source.AbsoluteFilePath)
            ? source.AbsoluteFilePath
            : (!string.IsNullOrWhiteSpace(absoluteFilePath) ? absoluteFilePath : source.RelativeFile);
        return Result<ToolsEditing.AssetClipboardPayload>.FromSuccess(new ToolsEditing.AssetClipboardPayload(
            toolsKind,
            source.Id,
            source.RootName,
            sourcePath,
            source.TableIndex,
            RawTomlText: source.RawTomlOverride));
    }

    /// <inheritdoc/>
    public Result<ToolsEditing.AssetCloneResult> DuplicateAsset(
        AssetKind kind,
        string assetId,
        string? absoluteFilePath,
        int tableIndex)
    {
        var source = FindSampleAsset(kind, assetId, absoluteFilePath);
        if (source == null)
        {
            return Result<ToolsEditing.AssetCloneResult>.FromFailure($"Could not find {kind} '{assetId}'.");
        }

        if (_lastOpenedMode == WorkspaceMode.Pack && source.Provenance == AssetProvenance.Base)
        {
            return Result<ToolsEditing.AssetCloneResult>.FromFailure(
                $"Cannot duplicate into read-only base asset file '{source.RelativeFile}'.");
        }

        var captured = CaptureAssetClipboard(kind, source.Id, absoluteFilePath ?? source.AbsoluteFilePath, tableIndex);
        if (!captured.Success)
        {
            return Result<ToolsEditing.AssetCloneResult>.FromFailure(captured.Message);
        }

        return PasteSampleSeed(captured.Value, source, targetFilePath: source.RelativeFile);
    }

    /// <inheritdoc/>
    public Result<ToolsEditing.AssetCloneResult> PasteAsset(
        ToolsEditing.AssetClipboardPayload payload,
        string? targetFilePath = null)
    {
        var studioKind = AssetSeedProjector.ToStudioAssetKind(payload.AssetKind);
        var source = FindSampleAsset(studioKind, payload.SourceAssetId, payload.SourceFilePath);
        if (source == null)
        {
            return Result<ToolsEditing.AssetCloneResult>.FromFailure(
                $"Source asset '{payload.SourceAssetId}' is no longer available in this session.");
        }

        return PasteSampleSeed(payload, source, targetFilePath);
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> GetOrderedAssetRootDirectories() => [];

    /// <inheritdoc/>
    public string ResolveImagePathForWorkspace(string absoluteImagePath) =>
        ResolveRelativeMarkerPath(absoluteImagePath, "/assets/textures/");

    /// <inheritdoc/>
    public string ResolveMeshPathForWorkspace(string absoluteMeshPath) =>
        ResolveRelativeMarkerPath(absoluteMeshPath, "/assets/meshes/");

    private string ResolveRelativeMarkerPath(string absolutePath, string preferredSubdirMarker)
    {
        if (string.IsNullOrWhiteSpace(absolutePath))
        {
            return string.Empty;
        }

        var normalized = absolutePath.Trim().Replace('\\', '/');
        var preferredIdx = normalized.IndexOf(preferredSubdirMarker, StringComparison.OrdinalIgnoreCase);
        if (preferredIdx >= 0)
        {
            return normalized.Substring(preferredIdx + preferredSubdirMarker.Length);
        }

        var assetsMarker = normalized.IndexOf("/assets/", StringComparison.OrdinalIgnoreCase);
        if (assetsMarker >= 0)
        {
            return normalized.Substring(assetsMarker + "/assets/".Length);
        }

        if (!string.IsNullOrWhiteSpace(_lastOpenedRootPath))
        {
            var normalizedRoot = _lastOpenedRootPath.Trim().Replace('\\', '/').TrimEnd('/') + "/";
            if (normalized.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                return normalized.Substring(normalizedRoot.Length);
            }
        }

        return normalized;
    }

    /// <inheritdoc/>
    public string ResolveBlockLayerNameForWorkspace(string absoluteImagePath)
    {
        if (string.IsNullOrWhiteSpace(absoluteImagePath))
        {
            return string.Empty;
        }

        var fileName = Path.GetFileName(absoluteImagePath.Trim().Replace('\\', '/'));
        return string.IsNullOrWhiteSpace(fileName)
            ? string.Empty
            : Path.GetFileNameWithoutExtension(fileName);
    }

    /// <inheritdoc/>
    public IReadOnlyList<CommandPaletteEntry> SearchCommandPalette(
        string query,
        string categoryFilter,
        string scopeFilter,
        int maxResults = 60) => [];

    /// <inheritdoc/>
    public void MarkCleanShutdown()
    {
    }

    private static List<StudioAssetSeed> BuildSampleAssets(WorkspaceMode mode, string rootLabel)
    {
        var packProv = mode == WorkspaceMode.Repo ? AssetProvenance.Repo : AssetProvenance.Pack;
        var overrideProv = mode == WorkspaceMode.Repo ? AssetProvenance.Repo : AssetProvenance.Override;
        var baseProv = mode == WorkspaceMode.Repo ? AssetProvenance.Repo : AssetProvenance.Base;
        var baseRoot = mode == WorkspaceMode.Repo ? rootLabel : "install (base)";

        return
        [
            new StudioAssetSeed
            {
                Id = "wb:thruster",
                Kind = AssetKind.Brick,
                GroupTitle = "Bricks",
                RelativeFile = "assets/bricks/thruster.toml",
                RootName = rootLabel,
                Provenance = overrideProv,
                ShadowedRoot = mode == WorkspaceMode.Pack ? "base" : null,
                IsDirty = true,
                ScalarValues = new Dictionary<string, string>
                {
                    ["Shape"] = "Custom",
                    ["Color"] = "#4A6B8A",
                    ["Alpha"] = "Opaque",
                    ["CastShadows"] = "true",
                    ["Passable"] = "false",
                    ["Mesh"] = "thruster.obj",
                    ["Connected"] = "false",
                    ["TextureDefault"] = "thruster_nozzle",
                    ["TextureTop"] = "vent",
                    ["TextureBottom"] = "vent",
                    ["TextureFront"] = "core",
                    ["TextureBack"] = "greeble1",
                    ["TextureLeft"] = "vent",
                    ["TextureRight"] = "vent",
                    ["SoundPlace"] = "metal_place",
                    ["SoundBreak"] = "metal_break",
                },
                Colliders =
                [
                    ("box", "1.0, 1.0, 0.1875", "0.0, 0.0, -0.40625", "0, 0, 0"),
                    ("box", "0.75, 0.75, 0.8125", "0.0, 0.0, 0.09375", "0, 0, 0"),
                ],
                InboundReferences =
                [
                    new InboundReferenceItem("wb:thruster_item", AssetKind.Item, "Placeable.ID", "assets/items/thruster.toml"),
                    new InboundReferenceItem("brick.paintable", AssetKind.Tag, "Tags.brick.paintable", "assets/lang/tags/bricks.toml"),
                    new InboundReferenceItem("building", AssetKind.Skill, "Sources.Place", "assets/skills/building.toml"),
                ],
            },
            new StudioAssetSeed
            {
                Id = "wb:panel",
                Kind = AssetKind.Brick,
                GroupTitle = "Bricks",
                RelativeFile = "assets/bricks/panel.toml",
                RootName = baseRoot,
                Provenance = baseProv,
                ScalarValues = new Dictionary<string, string>
                {
                    ["Shape"] = "Any",
                    ["Color"] = "#5A6988",
                    ["Alpha"] = "Opaque",
                    ["CastShadows"] = "true",
                    ["Passable"] = "false",
                    ["Mesh"] = "",
                    ["Connected"] = "true",
                    ["TextureDefault"] = "panel.0",
                    ["TextureTop"] = "",
                    ["TextureBottom"] = "",
                    ["TextureFront"] = "",
                    ["TextureBack"] = "",
                    ["TextureLeft"] = "",
                    ["TextureRight"] = "",
                    ["SoundPlace"] = "metal_place",
                    ["SoundBreak"] = "metal_break",
                },
                InboundReferences =
                [
                    new InboundReferenceItem("brick.paintable", AssetKind.Tag, "Tags.brick.paintable", "assets/lang/tags/bricks.toml"),
                    new InboundReferenceItem("ships/scout_mk1.wbp", AssetKind.Blueprint, "Palette[1]", "assets/blueprints/ships/scout_mk1.wbp"),
                ],
            },
            new StudioAssetSeed
            {
                Id = "wb:glass",
                Kind = AssetKind.Brick,
                GroupTitle = "Bricks",
                RelativeFile = "assets/bricks/glass.toml",
                RootName = baseRoot,
                Provenance = baseProv,
                ScalarValues = new Dictionary<string, string>
                {
                    ["Shape"] = "Block",
                    ["Color"] = "#88C0E8",
                    ["Alpha"] = "Blend",
                    ["CastShadows"] = "false",
                    ["Passable"] = "false",
                    ["Mesh"] = "",
                    ["Connected"] = "true",
                    ["TextureDefault"] = "glass.0",
                    ["TextureTop"] = "",
                    ["TextureBottom"] = "",
                    ["TextureFront"] = "",
                    ["TextureBack"] = "",
                    ["TextureLeft"] = "",
                    ["TextureRight"] = "",
                    ["SoundPlace"] = "glass_place",
                    ["SoundBreak"] = "glass_break",
                },
                InboundReferences =
                [
                    new InboundReferenceItem("brick.paintable", AssetKind.Tag, "Tags.brick.paintable", "assets/lang/tags/bricks.toml"),
                ],
            },
            new StudioAssetSeed
            {
                Id = "colony:broken_hull",
                Kind = AssetKind.Brick,
                GroupTitle = "Bricks",
                RelativeFile = "assets/bricks/broken_hull.toml",
                RootName = rootLabel,
                Provenance = packProv,
                HasSyntaxError = true,
                SyntaxErrorMessage = "Unterminated string literal in Shape field. Form editing is disabled until syntax is valid; raw text still saves.",
                RawTomlOverride = """
                    [[Bricks]]
                    ID      = "colony:broken_hull"
                    # Missing closing quote triggers raw-only syntax-error mode
                    Shape   = "block
                    Color   = "#6A7D92"
                    [Bricks.Textures]
                    Default = ["panel.0"]
                    """,
            },
            new StudioAssetSeed
            {
                Id = "wb:thruster_item",
                Kind = AssetKind.Item,
                GroupTitle = "Items",
                RelativeFile = "assets/items/thruster.toml",
                RootName = rootLabel,
                Provenance = packProv,
                IsDirty = true,
                ScalarValues = new Dictionary<string, string>
                {
                    ["Name"] = "item.base.thruster",
                    ["Description"] = "item.base.thruster.description",
                    ["Icon"] = "items/Thruster.png",
                    ["MaxStack"] = "100",
                    ["PlaceableType"] = "brick",
                    ["PlaceableID"] = "wb:thruster",
                    ["ToolType"] = "none",
                    ["ToolTarget"] = "",
                    ["ViewModelMesh"] = "",
                    ["ViewModelMaterial"] = "",
                    ["ViewModelPosition"] = "0.4, -0.4, -0.5",
                    ["ViewModelRotation"] = "0, -60, 0",
                    ["ViewModelScale"] = "0.25, 0.25, 0.25",
                    ["WorldModelMesh"] = "cube.obj",
                    ["WorldModelMaterial"] = "bricks/thruster",
                },
                InboundReferences =
                [
                    new InboundReferenceItem("lang/en/item.toml", AssetKind.Localization, "item.base.thruster", "assets/lang/en/item.toml"),
                ],
            },
            new StudioAssetSeed
            {
                Id = "wb:drill",
                Kind = AssetKind.Item,
                GroupTitle = "Items",
                RelativeFile = "assets/items/drill.toml",
                RootName = baseRoot,
                Provenance = baseProv,
                ScalarValues = new Dictionary<string, string>
                {
                    ["Name"] = "item.base.drill",
                    ["Description"] = "item.base.drill.description",
                    ["Icon"] = "items/drill.png",
                    ["MaxStack"] = "1",
                    ["PlaceableType"] = "none",
                    ["PlaceableID"] = "",
                    ["ToolType"] = "drill",
                    ["ToolTarget"] = "surface",
                    ["ViewModelMesh"] = "items/drill.obj",
                    ["ViewModelMaterial"] = "items/drill",
                    ["ViewModelPosition"] = "0.35, -0.32, -0.48",
                    ["ViewModelRotation"] = "0, -45, 0",
                    ["ViewModelScale"] = "0.3, 0.3, 0.3",
                    ["WorldModelMesh"] = "items/drill.obj",
                    ["WorldModelMaterial"] = "items/drill",
                },
                InboundReferences =
                [
                    new InboundReferenceItem("lang/en/item.toml", AssetKind.Localization, "item.base.drill", "assets/lang/en/item.toml"),
                ],
            },
            new StudioAssetSeed
            {
                Id = "wb:red",
                Kind = AssetKind.Paint,
                GroupTitle = "Paints (colors.toml · 32)",
                RelativeFile = "assets/paints/colors.toml",
                RootName = baseRoot,
                Provenance = baseProv,
                MultiEntryLabel = "colors.toml · Entry 9 of 32",
                ScalarValues = new Dictionary<string, string>
                {
                    ["Name"] = "paint.red.name",
                    ["PaintKind"] = "Color",
                    ["Color"] = "#E43B44",
                    ["Alpha"] = "Opaque",
                    ["Connected"] = "false",
                    ["Variants"] = "",
                },
                InboundReferences =
                [
                    new InboundReferenceItem("brick.paintable", AssetKind.Tag, "paint.endesga-32", "assets/lang/tags/paints.toml"),
                ],
            },
            new StudioAssetSeed
            {
                Id = "wb:panel_paint",
                Kind = AssetKind.Paint,
                GroupTitle = "Paints (textures.toml · 25)",
                RelativeFile = "assets/paints/textures.toml",
                RootName = rootLabel,
                Provenance = overrideProv,
                ShadowedRoot = mode == WorkspaceMode.Pack ? "base" : null,
                MultiEntryLabel = "textures.toml · Entry 1 of 25",
                ScalarValues = new Dictionary<string, string>
                {
                    ["Name"] = "paint.panel.name",
                    ["PaintKind"] = "Texture",
                    ["Color"] = "#8B9BB4",
                    ["Alpha"] = "Opaque",
                    ["Connected"] = "true",
                    ["Variants"] = "panel.0, panel.1, panel.2",
                },
                InboundReferences =
                [
                    new InboundReferenceItem("wb:panel", AssetKind.Brick, "Bricks.Textures.Default", "assets/bricks/panel.toml"),
                ],
            },
            new StudioAssetSeed
            {
                Id = "building",
                Kind = AssetKind.Skill,
                GroupTitle = "Skills",
                RelativeFile = "assets/skills/building.toml",
                RootName = rootLabel,
                Provenance = packProv,
                ScalarValues = new Dictionary<string, string>
                {
                    ["Name"] = "skill.name.building",
                    ["Category"] = "skill.category.misc",
                    ["Icon"] = "items/panel.png",
                },
                SkillSources =
                [
                    ("Place", "tag:brick.buildable", 1),
                    ("Place", "wb:thruster", 5),
                    ("Break", "tag:brick.manufactured", 2),
                ],
                SkillLevels =
                [
                    (1, 61),
                    (2, 105),
                    (3, 186),
                    (4, 308),
                    (5, 474),
                    (6, 685),
                    (7, 945),
                    (8, 1254),
                    (9, 1614),
                    (10, 2026),
                    (11, 2492),
                    (12, 3012),
                ],
                InboundReferences =
                [
                    new InboundReferenceItem("brick.paintable", AssetKind.Tag, "tag:brick.buildable", "assets/lang/tags/bricks.toml"),
                ],
            },
            new StudioAssetSeed
            {
                Id = "wb:human_m",
                Kind = AssetKind.Body,
                GroupTitle = "Bodies",
                RelativeFile = "assets/bodies/human_m.toml",
                RootName = rootLabel,
                Provenance = packProv,
                ScalarValues = new Dictionary<string, string>
                {
                    ["Species"] = "human",
                    ["Gender"] = "Male",
                },
                BodySprites =
                [
                    ("standing", "front", "characters/human_m_standing.png", "#5282B8"),
                    ("standing", "back", "characters/human_m_standing_back.png", "#426C9C"),
                    ("standing", "left", "characters/human_m_standing_left.png", "#4B76A8"),
                    ("standing", "right", "characters/human_m_standing_right.png", "#5A8CC4"),
                    ("floating", "front", "characters/human_m_floating.png", "#3D9A8B"),
                    ("floating", "back", "characters/human_m_floating_back.png", "#307D70"),
                    ("floating", "left", "characters/human_m_floating_left.png", "#358A7B"),
                    ("floating", "right", "characters/human_m_floating_right.png", "#44A897"),
                ],
                InboundReferences =
                [
                    new InboundReferenceItem("lang/en/item.toml", AssetKind.Localization, "names.first.wb:human_m", "assets/lang/en/body.toml"),
                ],
            },
            new StudioAssetSeed
            {
                Id = "bricks/glass",
                Kind = AssetKind.Material,
                GroupTitle = "Materials",
                RelativeFile = "assets/materials/bricks/glass.toml",
                RootName = rootLabel,
                Provenance = packProv,
                ScalarValues = new Dictionary<string, string>
                {
                    ["Shader"] = "textured",
                    ["Textures"] = "block/glass.png",
                    ["AlphaMode"] = "Masked",
                    ["AlphaCutoff"] = "0.5",
                },
                InboundReferences =
                [
                    new InboundReferenceItem("wb:glass", AssetKind.Brick, "Brick.Alpha", "assets/bricks/glass.toml"),
                ],
            },
            new StudioAssetSeed
            {
                Id = "brick.paintable",
                Kind = AssetKind.Tag,
                GroupTitle = "Tags",
                RelativeFile = "assets/lang/tags/bricks.toml",
                RootName = rootLabel,
                Provenance = packProv,
                TagBaseValues = ["wb:core", "wb:glass", "wb:panel", "wb:truss"],
                TagFileValues = ["wb:panel", "wb:thruster", "wb:caution_panel", "wb:vent"],
                InboundReferences =
                [
                    new InboundReferenceItem("wb:thruster", AssetKind.Brick, "Brick.Paintable", "assets/bricks/thruster.toml"),
                    new InboundReferenceItem("wb:panel", AssetKind.Brick, "Brick.Paintable", "assets/bricks/panel.toml"),
                ],
            },
            new StudioAssetSeed
            {
                Id = "lang/en/item.toml",
                Kind = AssetKind.Localization,
                GroupTitle = "Localization",
                RelativeFile = "assets/lang/en/item.toml",
                RootName = rootLabel,
                Provenance = packProv,
                LocalizationEntries =
                [
                    ("item.base.thruster", "Thruster", "[!! Thŕûštêŕ ··· !!]"),
                    ("item.base.thruster.description", "Standard solid-fuel rocket propulsion", "[!! Štâñdâŕd šølîd-fûêl ŕøçkêt pŕøpûlšîøñ !!]"),
                    ("item.base.core", "Core", "[!! Çøŕê ··· !!]"),
                    ("item.base.drill", "Drill", "[!! Dŕîll ··· !!]"),
                    ("item.base.panel", "Panel", "[!! Pâñêl ··· !!]"),
                    ("item.base.projector", "Blueprint Projector", "[!! Blûêpŕîñt Pŕøjêçtøŕ !!]"),
                ],
                InboundReferences =
                [
                    new InboundReferenceItem("wb:thruster_item", AssetKind.Item, "Item.Name", "assets/items/thruster.toml"),
                    new InboundReferenceItem("wb:drill", AssetKind.Item, "Item.Name", "assets/items/drill.toml"),
                ],
            },
            new StudioAssetSeed
            {
                Id = "ships/scout_mk1.wbp",
                Kind = AssetKind.Blueprint,
                GroupTitle = "Blueprints",
                RelativeFile = "assets/blueprints/ships/scout_mk1.wbp",
                RootName = rootLabel,
                Provenance = packProv,
                ScalarValues = new Dictionary<string, string>
                {
                    ["BlueprintName"] = "Scout Hull Mk.I",
                    ["SizeLabel"] = "18 × 7 × 24 voxels",
                    ["BrickCountLabel"] = "412 bricks · 6 palette entries",
                    ["FileSizeLabel"] = "14.8 KiB (limit 4 MiB)",
                    ["CreatedAtLabel"] = "2026-11-18 14:22 UTC",
                    ["ModifiedAtLabel"] = "2026-11-25 21:08 UTC",
                },
                BlueprintPalette =
                [
                    new BlueprintPaletteItem("wb:panel", AssetKind.Brick, 318, true, "318 voxel(s)"),
                    new BlueprintPaletteItem("wb:glass", AssetKind.Brick, 64, true, "64 voxel(s)"),
                    new BlueprintPaletteItem("wb:thruster", AssetKind.Brick, 30, true, "30 voxel(s)"),
                    new BlueprintPaletteItem("wb:red", AssetKind.Paint, 48, true, "48 face(s)"),
                ],
                InboundReferences =
                [
                    new InboundReferenceItem("wb:panel", AssetKind.Brick, "318 voxel(s)", "assets/bricks/panel.toml"),
                    new InboundReferenceItem("wb:glass", AssetKind.Brick, "64 voxel(s)", "assets/bricks/glass.toml"),
                    new InboundReferenceItem("wb:thruster", AssetKind.Brick, "30 voxel(s)", "assets/bricks/thruster.toml"),
                    new InboundReferenceItem("wb:red", AssetKind.Paint, "48 face(s)", "assets/paints/colors.toml"),
                ],
            },
        ];
    }

    private static List<ValidationFinding> BuildInitialFindings(string rootLabel)
    {
        return
        [
            new ValidationFinding(
                Id: "F-001",
                Severity: ValidationSeverity.Fatal,
                RootName: rootLabel,
                RelativeFile: "assets/bricks/broken_hull.toml",
                LineNumber: 4,
                AssetId: "colony:broken_hull",
                FieldKey: "Shape",
                Message: "Unterminated string literal in Shape field (opened directly in editable raw text mode)",
                Code: "TOML001"),
            new ValidationFinding(
                Id: "B-001",
                Severity: ValidationSeverity.Broken,
                RootName: rootLabel,
                RelativeFile: "assets/bricks/thruster.toml",
                LineNumber: 12,
                AssetId: "wb:thruster",
                FieldKey: "TextureDefault",
                Message: "Texture layer 'thruster_nozzle' not found in block textures (falls back to layer 0)",
                SuggestedFix: "vent",
                Code: "REF001",
                QuickFixKind: ValidationQuickFixKind.ReplaceReference,
                QuickFixLabel: "Replace with \"vent\""),
            new ValidationFinding(
                Id: "X-001",
                Severity: ValidationSeverity.CrossPlatform,
                RootName: rootLabel,
                RelativeFile: "assets/items/thruster.toml",
                LineNumber: 5,
                AssetId: "wb:thruster_item",
                FieldKey: "Icon",
                Message: "Case-only path mismatch: 'items/Thruster.png' differs from disk 'items/thruster.png'",
                CanFixCase: true,
                SuggestedFix: "items/thruster.png",
                Code: "PATH001",
                QuickFixKind: ValidationQuickFixKind.FixPathCase,
                QuickFixLabel: "Fix path casing (\"items/thruster.png\")",
                IsSafeBulkFix: true,
                FixDescription: "Fix path casing (\"items/thruster.png\")"),
            new ValidationFinding(
                Id: "S-001",
                Severity: ValidationSeverity.Style,
                RootName: rootLabel,
                RelativeFile: "assets/lang/tags/bricks.toml",
                LineNumber: 5,
                AssetId: "brick.paintable",
                FieldKey: null,
                Message: "Tag order matters; prefer appending new entries at the end",
                Code: "STYLE001"),
        ];
    }

    private static List<ReferenceCandidate> BuildCandidates(string rootLabel)
    {
        return
        [
            new ReferenceCandidate("vent", ReferenceKind.BrickFace, AssetProvenance.Base, ReferenceResolutionState.Resolved, "install", "block texture #14"),
            new ReferenceCandidate("core", ReferenceKind.BrickFace, AssetProvenance.Base, ReferenceResolutionState.Resolved, "install", "block texture #3"),
            new ReferenceCandidate("panel.0", ReferenceKind.BrickFace, AssetProvenance.Base, ReferenceResolutionState.Resolved, "install", "block texture #9 (connected)"),
            new ReferenceCandidate("greeble1", ReferenceKind.BrickFace, AssetProvenance.Base, ReferenceResolutionState.Resolved, "install", "block texture #7"),
            new ReferenceCandidate("glass.0", ReferenceKind.BrickFace, AssetProvenance.Base, ReferenceResolutionState.Resolved, "install", "block texture #5 (connected)"),
            new ReferenceCandidate("thruster.obj", ReferenceKind.Mesh, AssetProvenance.Base, ReferenceResolutionState.Resolved, "install", "assets/meshes/thruster.obj"),
            new ReferenceCandidate("items/drill.obj", ReferenceKind.Mesh, AssetProvenance.Base, ReferenceResolutionState.Resolved, "install", "assets/meshes/items/drill.obj"),
            new ReferenceCandidate("cube.obj", ReferenceKind.Mesh, AssetProvenance.Base, ReferenceResolutionState.Resolved, "install", "assets/meshes/cube.obj"),
            new ReferenceCandidate("items/thruster.png", ReferenceKind.Texture, AssetProvenance.Pack, ReferenceResolutionState.Resolved, rootLabel, "64×64 RGBA PNG"),
            new ReferenceCandidate("items/drill.png", ReferenceKind.Texture, AssetProvenance.Base, ReferenceResolutionState.Resolved, "install", "64×64 RGBA PNG"),
            new ReferenceCandidate("items/panel.png", ReferenceKind.Texture, AssetProvenance.Base, ReferenceResolutionState.Resolved, "install", "64×64 RGBA PNG"),
            new ReferenceCandidate("block/glass.png", ReferenceKind.Texture, AssetProvenance.Base, ReferenceResolutionState.Resolved, "install", "16×16 RGBA PNG"),
            new ReferenceCandidate("bricks/light", ReferenceKind.Material, AssetProvenance.Base, ReferenceResolutionState.Resolved, "install", "assets/materials/bricks/light.toml"),
            new ReferenceCandidate("bricks/thruster", ReferenceKind.Material, AssetProvenance.Base, ReferenceResolutionState.Resolved, "install", "assets/materials/bricks/thruster.toml"),
            new ReferenceCandidate("bricks/glass", ReferenceKind.Material, AssetProvenance.Pack, ReferenceResolutionState.Resolved, rootLabel, "assets/materials/bricks/glass.toml"),
            new ReferenceCandidate("items/drill", ReferenceKind.Material, AssetProvenance.Base, ReferenceResolutionState.Resolved, "install", "assets/materials/items/drill.toml"),
            new ReferenceCandidate("metal_place", ReferenceKind.Audio, AssetProvenance.Base, ReferenceResolutionState.Resolved, "install", "sounds/place/metal/metal_place.1.wav"),
            new ReferenceCandidate("metal_break", ReferenceKind.Audio, AssetProvenance.Base, ReferenceResolutionState.Resolved, "install", "sounds/break/metal/metal_break.1.wav"),
            new ReferenceCandidate("glass_place", ReferenceKind.Audio, AssetProvenance.Base, ReferenceResolutionState.Resolved, "install", "sounds/place/glass/glass_place.1.wav"),
            new ReferenceCandidate("glass_break", ReferenceKind.Audio, AssetProvenance.Base, ReferenceResolutionState.Resolved, "install", "sounds/break/glass/glass_break.1.wav"),
            new ReferenceCandidate("metal_place", ReferenceKind.SoundTag, AssetProvenance.Base, ReferenceResolutionState.Resolved, "install", "3 clip(s) · assets/lang/tags/sounds.toml"),
            new ReferenceCandidate("metal_break", ReferenceKind.SoundTag, AssetProvenance.Base, ReferenceResolutionState.Resolved, "install", "3 clip(s) · assets/lang/tags/sounds.toml"),
            new ReferenceCandidate("rock_place", ReferenceKind.SoundTag, AssetProvenance.Base, ReferenceResolutionState.Resolved, "install", "3 clip(s) · assets/lang/tags/sounds.toml"),
            new ReferenceCandidate("rock_break", ReferenceKind.SoundTag, AssetProvenance.Base, ReferenceResolutionState.Resolved, "install", "1 clip(s) · assets/lang/tags/sounds.toml"),
            new ReferenceCandidate("ui_click", ReferenceKind.SoundTag, AssetProvenance.Base, ReferenceResolutionState.Resolved, "install", "1 clip(s) · assets/lang/tags/sounds.toml"),
            new ReferenceCandidate("glass_place", ReferenceKind.SoundTag, AssetProvenance.Base, ReferenceResolutionState.Resolved, "install", "1 clip(s) · assets/lang/tags/sounds.toml"),
            new ReferenceCandidate("glass_break", ReferenceKind.SoundTag, AssetProvenance.Base, ReferenceResolutionState.Resolved, "install", "1 clip(s) · assets/lang/tags/sounds.toml"),
            new ReferenceCandidate("item.base.thruster", ReferenceKind.Localization, AssetProvenance.Pack, ReferenceResolutionState.Resolved, rootLabel, "Thruster"),
            new ReferenceCandidate("item.base.thruster.description", ReferenceKind.Localization, AssetProvenance.Pack, ReferenceResolutionState.Resolved, rootLabel, "Standard solid-fuel rocket propulsion"),
            new ReferenceCandidate("item.base.drill", ReferenceKind.Localization, AssetProvenance.Base, ReferenceResolutionState.Resolved, "install", "Drill"),
            new ReferenceCandidate("item.base.drill.description", ReferenceKind.Localization, AssetProvenance.Base, ReferenceResolutionState.Resolved, "install", "Handheld tool for modifying surfaces"),
            new ReferenceCandidate("item.base.core", ReferenceKind.Localization, AssetProvenance.Base, ReferenceResolutionState.Resolved, "install", "Core"),
            new ReferenceCandidate("paint.red.name", ReferenceKind.Localization, AssetProvenance.Base, ReferenceResolutionState.Resolved, "install", "Red"),
            new ReferenceCandidate("paint.panel.name", ReferenceKind.Localization, AssetProvenance.Base, ReferenceResolutionState.Resolved, "install", "Panel"),
            new ReferenceCandidate("skill.name.building", ReferenceKind.Localization, AssetProvenance.Base, ReferenceResolutionState.Resolved, "install", "Building"),
            new ReferenceCandidate("skill.category.misc", ReferenceKind.Localization, AssetProvenance.Base, ReferenceResolutionState.Resolved, "install", "Miscellaneous"),
            new ReferenceCandidate("wb:thruster", ReferenceKind.BrickId, AssetProvenance.Override, ReferenceResolutionState.Resolved, rootLabel, "Custom Brick"),
            new ReferenceCandidate("wb:panel", ReferenceKind.BrickId, AssetProvenance.Base, ReferenceResolutionState.Resolved, "install", "Any Shape Brick"),
            new ReferenceCandidate("wb:glass", ReferenceKind.BrickId, AssetProvenance.Base, ReferenceResolutionState.Resolved, "install", "Blend Block Brick"),
            new ReferenceCandidate("wb:core", ReferenceKind.BrickId, AssetProvenance.Base, ReferenceResolutionState.Resolved, "install", "Block Brick"),
            new ReferenceCandidate("brick.paintable", ReferenceKind.Tag, AssetProvenance.Pack, ReferenceResolutionState.Resolved, rootLabel, "7 effective values"),
            new ReferenceCandidate("brick.buildable", ReferenceKind.Tag, AssetProvenance.Base, ReferenceResolutionState.Resolved, "install", "19 effective values"),
        ];
    }

    private static List<WorkspaceImageEntry> BuildSampleWorkspaceImages(string rootPath)
    {
        var baseDir = string.IsNullOrWhiteSpace(rootPath)
            ? "~/packs/my_colony"
            : rootPath.TrimEnd('/', '\\').Replace('\\', '/');
        return
        [
            new WorkspaceImageEntry("characters/human_m_floating.png", "human_m_floating.png", $"{baseDir}/assets/textures/characters/human_m_floating.png", "characters"),
            new WorkspaceImageEntry("characters/human_m_floating_back.png", "human_m_floating_back.png", $"{baseDir}/assets/textures/characters/human_m_floating_back.png", "characters"),
            new WorkspaceImageEntry("characters/human_m_standing.png", "human_m_standing.png", $"{baseDir}/assets/textures/characters/human_m_standing.png", "characters"),
            new WorkspaceImageEntry("characters/human_m_standing_back.png", "human_m_standing_back.png", $"{baseDir}/assets/textures/characters/human_m_standing_back.png", "characters"),
            new WorkspaceImageEntry("characters/human_m_standing_left.png", "human_m_standing_left.png", $"{baseDir}/assets/textures/characters/human_m_standing_left.png", "characters"),
            new WorkspaceImageEntry("characters/human_m_standing_right.png", "human_m_standing_right.png", $"{baseDir}/assets/textures/characters/human_m_standing_right.png", "characters"),
            new WorkspaceImageEntry("items/drill.png", "drill.png", $"{baseDir}/assets/textures/items/drill.png", "items"),
            new WorkspaceImageEntry("items/panel.png", "panel.png", $"{baseDir}/assets/textures/items/panel.png", "items"),
            new WorkspaceImageEntry("items/thruster.png", "thruster.png", $"{baseDir}/assets/textures/items/thruster.png", "items"),
            new WorkspaceImageEntry("block/glass.png", "glass.png", $"{baseDir}/assets/textures/block/glass.png", "block"),
            new WorkspaceImageEntry("block/panel.0.png", "panel.0.png", $"{baseDir}/assets/textures/block/panel.0.png", "block"),
        ];
    }

    private static List<DraftRecoveryEntry> BuildPendingDrafts()
    {
        return
        [
            new DraftRecoveryEntry(
                RelativeFile: "assets/bricks/thruster.toml",
                SavedAtLabel: "2026-11-26 00:18 UTC",
                BaseHashChanged: false,
                Summary: "Modified TextureDefault and collider box extents"),
            new DraftRecoveryEntry(
                RelativeFile: "assets/items/thruster.toml",
                SavedAtLabel: "2026-11-26 00:16 UTC",
                BaseHashChanged: true,
                Summary: "Base file changed on disk since draft snapshot (no auto-merge)"),
        ];
    }

    private StudioAssetSeed? FindSampleAsset(AssetKind kind, string assetId, string? filePath)
    {
        if (_sessionAssets == null)
        {
            return null;
        }

        for (var i = 0; i < _sessionAssets.Count; i++)
        {
            var candidate = _sessionAssets[i];
            if (candidate.Kind == kind
                && string.Equals(candidate.Id, assetId, StringComparison.OrdinalIgnoreCase)
                && (string.IsNullOrWhiteSpace(filePath)
                    || string.Equals(candidate.RelativeFile, filePath, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(candidate.AbsoluteFilePath, filePath, StringComparison.OrdinalIgnoreCase)))
            {
                return candidate;
            }
        }

        for (var i = 0; i < _sessionAssets.Count; i++)
        {
            var candidate = _sessionAssets[i];
            if (candidate.Kind == kind && string.Equals(candidate.Id, assetId, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return null;
    }

    private Result<ToolsEditing.AssetCloneResult> PasteSampleSeed(
        ToolsEditing.AssetClipboardPayload payload,
        StudioAssetSeed source,
        string? targetFilePath)
    {
        if (_sessionAssets == null)
        {
            return Result<ToolsEditing.AssetCloneResult>.FromFailure("No sample workspace is open.");
        }

        var studioKind = source.Kind;
        var newId = AllocateSampleAssetId(studioKind, source.Id);
        var effectiveTargetFile = !string.IsNullOrWhiteSpace(targetFilePath)
            ? targetFilePath.Replace('\\', '/')
            : source.RelativeFile;

        if (studioKind == AssetKind.Material)
        {
            effectiveTargetFile = $"assets/materials/{newId}.toml";
        }
        else if (studioKind == AssetKind.Blueprint)
        {
            var fileId = newId.EndsWith(".wbp", StringComparison.OrdinalIgnoreCase) ? newId : $"{newId}.wbp";
            effectiveTargetFile = $"assets/blueprints/{fileId}";
        }

        var clonedProvenance = _lastOpenedMode == WorkspaceMode.Repo ? AssetProvenance.Repo : AssetProvenance.Pack;
        var clonedScalars = new Dictionary<string, string>(source.ScalarValues, StringComparer.Ordinal);
        if (studioKind == AssetKind.Blueprint)
        {
            clonedScalars["BlueprintName"] = newId;
        }

        var clonedSeed = new StudioAssetSeed
        {
            Id = newId,
            Kind = studioKind,
            GroupTitle = source.GroupTitle,
            RelativeFile = effectiveTargetFile,
            RootName = _sessionRootLabel,
            Provenance = clonedProvenance,
            IsDirty = false,
            HasSyntaxError = source.HasSyntaxError,
            SyntaxErrorMessage = source.SyntaxErrorMessage,
            RawTomlOverride = source.RawTomlOverride,
            AbsoluteFilePath = source.AbsoluteFilePath,
            TableIndex = source.TableIndex,
            EntryIndex = _sessionAssets.Count,
            ResolvedBodyFrontTexturePath = source.ResolvedBodyFrontTexturePath,
            ResolvedMaterialTexturePath = source.ResolvedMaterialTexturePath,
            ResolvedPaintTexturePath = source.ResolvedPaintTexturePath,
            BodySpriteAbsolutePaths = new Dictionary<string, string>(source.BodySpriteAbsolutePaths, StringComparer.OrdinalIgnoreCase),
            BlueprintPalette = [.. source.BlueprintPalette],
            ScalarValues = clonedScalars,
            TagBaseValues = [.. source.TagBaseValues],
            TagFileValues = [.. source.TagFileValues],
            LocalizationEntries = [.. source.LocalizationEntries],
            Colliders = [.. source.Colliders],
            SkillSources = [.. source.SkillSources],
            SkillLevels = [.. source.SkillLevels],
            BodySprites = [.. source.BodySprites],
        };

        var sourceIndex = _sessionAssets.IndexOf(source);
        if (sourceIndex >= 0 && sourceIndex < _sessionAssets.Count - 1)
        {
            _sessionAssets.Insert(sourceIndex + 1, clonedSeed);
        }
        else
        {
            _sessionAssets.Add(clonedSeed);
        }

        return Result<ToolsEditing.AssetCloneResult>.FromSuccess(new ToolsEditing.AssetCloneResult(
            payload.AssetKind,
            source.Id,
            newId,
            _sessionRootLabel,
            effectiveTargetFile,
            NewTableIndex: clonedSeed.TableIndex,
            NewEntryIndex: clonedSeed.EntryIndex,
            CreatedNewFile: studioKind is AssetKind.Material or AssetKind.Blueprint));
    }

    private string AllocateSampleAssetId(AssetKind kind, string baseId)
    {
        var trimmed = baseId.Trim();
        var stem = ToolsEditing.AssetIdAllocator.ExtractBaseStem(trimmed);
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { trimmed };
        if (_sessionAssets != null)
        {
            for (var i = 0; i < _sessionAssets.Count; i++)
            {
                if (_sessionAssets[i].Kind == kind && !string.IsNullOrWhiteSpace(_sessionAssets[i].Id))
                {
                    taken.Add(_sessionAssets[i].Id);
                }
            }
        }

        for (var suffix = 2; suffix < int.MaxValue; suffix++)
        {
            var candidate = string.Concat(stem, "_", suffix.ToString(CultureInfo.InvariantCulture));
            if (!taken.Contains(candidate))
            {
                return candidate;
            }
        }

        return string.Concat(stem, "_copy");
    }

    private bool ApplySampleFindingToSessionAssets(ValidationFinding finding)
    {
        if (_sessionAssets == null)
        {
            return false;
        }

        var targetAsset = FindSampleAssetByFinding(finding);
        if (targetAsset == null)
        {
            return finding.QuickFixKind is ValidationQuickFixKind.StripUtf8Bom or ValidationQuickFixKind.NormalizeLineEndings or ValidationQuickFixKind.FormatFile;
        }

        if (!string.IsNullOrWhiteSpace(finding.FieldKey) && !string.IsNullOrWhiteSpace(finding.SuggestedFix))
        {
            targetAsset.ScalarValues[finding.FieldKey] = finding.SuggestedFix;
            return true;
        }

        if (finding.QuickFixKind == ValidationQuickFixKind.InsertDefaultSkillLevels && targetAsset.Kind == AssetKind.Skill)
        {
            targetAsset.SkillLevels.Clear();
            targetAsset.SkillLevels.Add((1, 100));
            return true;
        }

        return finding.QuickFixKind is ValidationQuickFixKind.StripUtf8Bom or ValidationQuickFixKind.NormalizeLineEndings or ValidationQuickFixKind.FormatFile;
    }

    private StudioAssetSeed? FindSampleAssetByFinding(in ValidationFinding finding)
    {
        if (_sessionAssets == null)
        {
            return null;
        }

        for (var i = 0; i < _sessionAssets.Count; i++)
        {
            var asset = _sessionAssets[i];
            if (!string.IsNullOrWhiteSpace(finding.AssetId)
                && string.Equals(asset.Id, finding.AssetId, StringComparison.OrdinalIgnoreCase))
            {
                return asset;
            }

            if (!string.IsNullOrWhiteSpace(finding.RelativeFile)
                && string.Equals(asset.RelativeFile, finding.RelativeFile, StringComparison.OrdinalIgnoreCase))
            {
                return asset;
            }
        }

        return null;
    }
}

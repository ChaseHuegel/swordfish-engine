using Swordfish.IO;
using Swordfish.Library.Util;
using WaywardBeyond.AssetTools.Documents;
using WaywardBeyond.Bodies;
using WaywardBeyond.Bricks;
using WaywardBeyond.Items;
using WaywardBeyond.Recipes;
using WaywardBeyond.Skills;

namespace WaywardBeyond.AssetTools.Workspace;

/// <summary>
///     Discovers module/pack roots in load order and builds a multi-root <see cref="WorkspaceIndex"/>.
/// </summary>
public sealed class WorkspaceLoader
{
    private static readonly string[] DEFAULT_LOAD_ORDER =
    [
        "swordfish",
        "waywardbeyond.bricks",
        "waywardbeyond.items",
        "waywardbeyond.recipes",
        "waywardbeyond.skills",
        "waywardbeyond.bodies",
        "waywardbeyond.client",
        "waywardbeyond.server"
    ];

    private static readonly string[] NON_ALBEDO_SUFFIXES =
    [
        ".m.png", ".s.png", ".n.png", ".e.png", ".t.png",
        ".m.bmp", ".s.bmp", ".n.bmp", ".e.bmp", ".t.bmp"
    ];

    private static readonly string[] TEXTURE_EXTENSIONS = [".png", ".bmp"];
    private static readonly string[] AUDIO_EXTENSIONS = [".wav", ".mp3", ".flac", ".ogg"];
    private static readonly string[] SHADER_EXTENSIONS = [".glsl", ".vert", ".frag"];

    /// <summary>
    ///     Loads a repository checkout workspace where enabled modules are writable in configured load order.
    /// </summary>
    public Result<WorkspaceIndex> LoadRepo(string repoRoot)
    {
        if (string.IsNullOrWhiteSpace(repoRoot) || !Directory.Exists(repoRoot))
        {
            return Result<WorkspaceIndex>.FromFailure($"Repository directory does not exist: '{repoRoot}'.");
        }

        string fullRepoRoot = Path.GetFullPath(repoRoot);
        ModulesConfigData? modulesConfig = TryLoadModulesConfig(fullRepoRoot);
        IReadOnlyList<string> loadOrder = modulesConfig.HasValue && modulesConfig.Value.LoadOrder.Count > 0
            ? modulesConfig.Value.LoadOrder
            : DEFAULT_LOAD_ORDER;

        List<WorkspaceRoot> roots = DiscoverModuleRoots(fullRepoRoot, loadOrder, isWritable: true);
        if (roots.Count == 0)
        {
            return Result<WorkspaceIndex>.FromFailure($"No modules with manifest.toml found under '{fullRepoRoot}'.");
        }

        return Result<WorkspaceIndex>.FromSuccess(BuildIndex(WorkspaceMode.Repo, fullRepoRoot, null, roots, modulesConfig));
    }

    /// <summary>
    ///     Loads a standalone content pack workspace layered over optional read-only game install roots.
    /// </summary>
    public Result<WorkspaceIndex> LoadPack(string packDirectory, string? gameInstallDirectory = null)
    {
        if (string.IsNullOrWhiteSpace(packDirectory) || !Directory.Exists(packDirectory))
        {
            return Result<WorkspaceIndex>.FromFailure($"Pack directory does not exist: '{packDirectory}'.");
        }

        string fullPackDir = Path.GetFullPath(packDirectory);
        string? fullInstallDir = !string.IsNullOrWhiteSpace(gameInstallDirectory)
            ? Path.GetFullPath(gameInstallDirectory)
            : null;

        if (fullInstallDir != null && !Directory.Exists(fullInstallDir))
        {
            return Result<WorkspaceIndex>.FromFailure($"Reference game install directory does not exist: '{fullInstallDir}'.");
        }

        List<WorkspaceRoot> roots = [];
        ModulesConfigData? modulesConfig = null;
        if (fullInstallDir != null)
        {
            modulesConfig = TryLoadModulesConfig(fullInstallDir);
            IReadOnlyList<string> loadOrder = modulesConfig.HasValue && modulesConfig.Value.LoadOrder.Count > 0
                ? modulesConfig.Value.LoadOrder
                : DEFAULT_LOAD_ORDER;

            string modulesContainer = Directory.Exists(Path.Combine(fullInstallDir, "modules"))
                ? Path.Combine(fullInstallDir, "modules")
                : fullInstallDir;
            roots.AddRange(DiscoverModuleRoots(modulesContainer, loadOrder, isWritable: false));
        }

        WorkspaceRoot packRoot = CreateSingleRoot(fullPackDir, isWritable: true, loadOrderIndex: roots.Count);
        roots.Add(packRoot);

        return Result<WorkspaceIndex>.FromSuccess(BuildIndex(WorkspaceMode.Pack, fullPackDir, fullInstallDir, roots, modulesConfig));
    }

    /// <summary>
    ///     Auto-detects whether <paramref name="rootPath"/> is a repository checkout or content pack and loads it.
    /// </summary>
    public Result<WorkspaceIndex> LoadAuto(string rootPath, string? gameInstallDirectory = null)
    {
        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
        {
            return Result<WorkspaceIndex>.FromFailure($"Workspace directory does not exist: '{rootPath}'.");
        }

        string fullPath = Path.GetFullPath(rootPath);
        if (IsPackDirectory(fullPath) && !File.Exists(Path.Combine(fullPath, "Swordfish.sln")))
        {
            return LoadPack(fullPath, gameInstallDirectory);
        }

        if (File.Exists(Path.Combine(fullPath, "Swordfish.sln")) ||
            TryLoadModulesConfig(fullPath).HasValue)
        {
            return LoadRepo(fullPath);
        }

        return LoadPack(fullPath, gameInstallDirectory);
    }

    /// <summary>
    ///     Parses a Shoal module <c>manifest.toml</c> file into a read-only data projection.
    /// </summary>
    public static Result<ModuleManifestData> LoadManifest(string manifestPath)
    {
        Result<TomlDocument> docResult = TomlDocument.Load(manifestPath);
        if (!docResult.Success)
        {
            return Result<ModuleManifestData>.FromFailure(docResult.Message);
        }

        TomlDocument doc = docResult.Value;
        if (doc.HasSyntaxErrors)
        {
            return Result<ModuleManifestData>.FromFailure($"Manifest '{manifestPath}' contains TOML syntax errors.");
        }

        string id = ReadRootString(doc, "ID");
        string name = ReadRootString(doc, "Name");
        string description = ReadRootString(doc, "Description");
        string author = ReadRootString(doc, "Author");
        string website = ReadRootString(doc, "Website");
        string source = ReadRootString(doc, "Source");
        string? rootOverride = ReadOptionalRootString(doc, "RootPathOverride");
        string? scriptsPath = ReadOptionalRootString(doc, "ScriptsPath");
        string? assembliesPath = ReadOptionalRootString(doc, "AssembliesPath");
        IReadOnlyList<string> assemblies = ReadRootStringArray(doc, "Assemblies");

        return Result<ModuleManifestData>.FromSuccess(new ModuleManifestData(
            id,
            name,
            description,
            author,
            website,
            source,
            rootOverride,
            scriptsPath,
            assembliesPath,
            assemblies));
    }

    /// <summary>
    ///     Parses a <c>config/modules.toml</c> file into a read-only data projection.
    /// </summary>
    public static Result<ModulesConfigData> LoadModulesConfig(string configPath)
    {
        Result<TomlDocument> docResult = TomlDocument.Load(configPath);
        if (!docResult.Success)
        {
            return Result<ModulesConfigData>.FromFailure(docResult.Message);
        }

        TomlDocument doc = docResult.Value;
        if (doc.HasSyntaxErrors)
        {
            return Result<ModulesConfigData>.FromFailure($"Modules config '{configPath}' contains TOML syntax errors.");
        }

        bool allowScripts = false;
        Result<TomlFieldSpan> allowField = doc.GetRootField("AllowScriptCompilation");
        if (allowField.Success && allowField.Value.TypedValue is bool flag)
        {
            allowScripts = flag;
        }

        IReadOnlyList<string> loadOrder = ReadRootStringArray(doc, "LoadOrder");
        return Result<ModulesConfigData>.FromSuccess(new ModulesConfigData(allowScripts, loadOrder, Path.GetFullPath(configPath)));
    }

    private static bool IsPackDirectory(string directory)
    {
        return File.Exists(Path.Combine(directory, "pack.toml")) ||
               File.Exists(Path.Combine(directory, "manifest.toml")) ||
               Directory.Exists(Path.Combine(directory, "assets"));
    }

    private static ModulesConfigData? TryLoadModulesConfig(string rootDirectory)
    {
        string[] candidates =
        [
            Path.Combine(rootDirectory, "config", "modules.toml"),
            Path.Combine(rootDirectory, "WaywardBeyond.Client.Launcher", "config", "modules.toml"),
            Path.Combine(rootDirectory, "assets", "config", "modules.toml")
        ];

        for (int i = 0; i < candidates.Length; i++)
        {
            if (!File.Exists(candidates[i]))
            {
                continue;
            }

            Result<ModulesConfigData> loaded = LoadModulesConfig(candidates[i]);
            if (loaded.Success)
            {
                return loaded.Value;
            }
        }

        return null;
    }

    private static List<WorkspaceRoot> DiscoverModuleRoots(
        string containerDirectory,
        IReadOnlyList<string> loadOrder,
        bool isWritable)
    {
        Dictionary<string, int> orderMap = new(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < loadOrder.Count; i++)
        {
            if (!orderMap.ContainsKey(loadOrder[i]))
            {
                orderMap[loadOrder[i]] = i;
            }
        }

        List<WorkspaceRoot> enabled = [];
        List<WorkspaceRoot> disabled = [];

        foreach (string subDir in Directory.EnumerateDirectories(containerDirectory))
        {
            string manifestPath = Path.Combine(subDir, "manifest.toml");
            if (!File.Exists(manifestPath))
            {
                continue;
            }

            Result<ModuleManifestData> manifestResult = LoadManifest(manifestPath);
            ModuleManifestData? manifest = manifestResult.Success ? manifestResult.Value : null;
            string id = manifest.HasValue && !string.IsNullOrWhiteSpace(manifest.Value.Id)
                ? manifest.Value.Id
                : Path.GetFileName(subDir);
            string name = manifest.HasValue && !string.IsNullOrWhiteSpace(manifest.Value.Name)
                ? manifest.Value.Name
                : id;

            string effectiveRoot = ResolveEffectiveRoot(subDir, manifest?.RootPathOverride);
            string assetsDir = Path.Combine(effectiveRoot, "assets");
            bool isEnabled = orderMap.TryGetValue(id, out int orderIndex);
            var root = new WorkspaceRoot(
                id,
                name,
                subDir,
                assetsDir,
                isWritable,
                isEnabled,
                isEnabled ? orderIndex : int.MaxValue,
                manifest);

            if (isEnabled)
            {
                enabled.Add(root);
            }
            else
            {
                disabled.Add(root);
            }
        }

        enabled.Sort(static (left, right) => left.LoadOrderIndex.CompareTo(right.LoadOrderIndex));
        disabled.Sort(static (left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.Id, right.Id));

        List<WorkspaceRoot> combined = new(enabled.Count + disabled.Count);
        combined.AddRange(enabled);
        combined.AddRange(disabled);
        return combined;
    }

    private static WorkspaceRoot CreateSingleRoot(string directory, bool isWritable, int loadOrderIndex)
    {
        string manifestPath = Path.Combine(directory, "manifest.toml");
        ModuleManifestData? manifest = null;
        if (File.Exists(manifestPath))
        {
            Result<ModuleManifestData> loaded = LoadManifest(manifestPath);
            if (loaded.Success)
            {
                manifest = loaded.Value;
            }
        }

        string id = manifest.HasValue && !string.IsNullOrWhiteSpace(manifest.Value.Id)
            ? manifest.Value.Id
            : Path.GetFileName(directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        string name = manifest.HasValue && !string.IsNullOrWhiteSpace(manifest.Value.Name)
            ? manifest.Value.Name
            : id;
        string effectiveRoot = ResolveEffectiveRoot(directory, manifest?.RootPathOverride);

        return new WorkspaceRoot(
            id,
            name,
            directory,
            Path.Combine(effectiveRoot, "assets"),
            isWritable,
            IsEnabled: true,
            loadOrderIndex,
            manifest);
    }

    private static string ResolveEffectiveRoot(string moduleDirectory, string? rootPathOverride)
    {
        if (string.IsNullOrWhiteSpace(rootPathOverride))
        {
            return moduleDirectory;
        }

        return Path.IsPathRooted(rootPathOverride)
            ? Path.GetFullPath(rootPathOverride)
            : Path.GetFullPath(Path.Combine(moduleDirectory, rootPathOverride));
    }

    private static WorkspaceIndex BuildIndex(
        WorkspaceMode mode,
        string rootPath,
        string? referenceInstallPath,
        IReadOnlyList<WorkspaceRoot> roots,
        ModulesConfigData? modulesConfig)
    {
        var state = new IndexBuilderState();

        for (int i = 0; i < roots.Count; i++)
        {
            WorkspaceRoot root = roots[i];
            if (!root.IsEnabled || !Directory.Exists(root.AssetsDirectory))
            {
                continue;
            }

            IndexRootAssets(in root, state);
            IndexRootResources(in root, state);
            IndexRootLocalizationAndTags(in root, state);
            IndexRootBlueprints(in root, state);
        }

        List<string> sortedAlbedo = [.. state.WinningBlockAlbedoLayers.Keys];
        sortedAlbedo.Sort(new NaturalComparer());
        List<string> orderedAlbedoNames = new(sortedAlbedo.Count + 1) { "missing" };
        orderedAlbedoNames.AddRange(sortedAlbedo);

        ExtractReferences(state);

        return new WorkspaceIndex(
            mode,
            rootPath,
            referenceInstallPath,
            roots,
            modulesConfig,
            state.AllAssets,
            state.WinningAssets,
            state.AllResources,
            state.WinningResources,
            state.WinningBlockAlbedoLayers,
            orderedAlbedoNames,
            state.LocalizationEntries,
            state.TagEntries,
            state.DocumentsByPath,
            state.AllReferences,
            state.BlueprintFiles);
    }

    private static void IndexRootAssets(in WorkspaceRoot root, IndexBuilderState state)
    {
        IndexArrayAssetDirectory<BrickDefinitions, BrickDefinition>(
            in root, "bricks", "Bricks", AssetKind.Brick,
            static defs => defs.Bricks,
            static def => (def.ID ?? string.Empty, def.ID ?? string.Empty),
            state);

        IndexArrayAssetDirectory<ItemDefinitions, ItemDefinition>(
            in root, "items", "Items", AssetKind.Item,
            static defs => defs.Items,
            static def => (def.ID ?? string.Empty, string.IsNullOrWhiteSpace(def.Name) ? (def.ID ?? string.Empty) : def.Name),
            state);

        IndexArrayAssetDirectory<PaintDefinitions, PaintDefinition>(
            in root, "paints", "Paints", AssetKind.Paint,
            static defs => defs.Paints,
            static def => (def.ID ?? string.Empty, string.IsNullOrWhiteSpace(def.Name) ? (def.ID ?? string.Empty) : def.Name),
            state);

        IndexArrayAssetDirectory<SkillDefinitions, SkillDefinition>(
            in root, "skills", "Skills", AssetKind.Skill,
            static defs => defs.Skills,
            static def => (def.ID ?? string.Empty, string.IsNullOrWhiteSpace(def.Name) ? (def.ID ?? string.Empty) : def.Name),
            state);

        IndexArrayAssetDirectory<BodyDefinitions, BodyDefinition>(
            in root, "bodies", "Bodies", AssetKind.Body,
            static defs => defs.Bodies,
            static def => (def.ID ?? string.Empty, def.ID ?? string.Empty),
            state);

        IndexArrayAssetDirectory<RecipeDefinitions, RecipeDefinition>(
            in root, "recipes", "Recipes", AssetKind.Recipe,
            static defs => defs.Recipes,
            static def => (def.ID ?? string.Empty, def.ID ?? string.Empty),
            state);

        IndexMaterialDirectory(in root, state);
    }

    private static void IndexArrayAssetDirectory<TContainer, TItem>(
        in WorkspaceRoot root,
        string subDirectory,
        string tableName,
        AssetKind kind,
        Func<TContainer, List<TItem>?> getItems,
        Func<TItem, (string Id, string DisplayName)> getIdentity,
        IndexBuilderState state)
    {
        string directory = Path.Combine(root.AssetsDirectory, subDirectory);
        if (!Directory.Exists(directory))
        {
            return;
        }

        string[] files = Directory.GetFiles(directory, "*.toml", SearchOption.AllDirectories);
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);

        for (int f = 0; f < files.Length; f++)
        {
            string filePath = Path.GetFullPath(files[f]);
            TomlDocument document = LoadAndTrackDocument(filePath, state);
            string relativePath = NormalizeRelativePath(root.AssetsDirectory, filePath);
            IReadOnlyList<TomlTableSpan> entryTables = document.GetTables(tableName);

            if (document.HasSyntaxErrors)
            {
                continue;
            }

            DualTomlParseResult<TContainer> parsed = DualTomlParser.Parse<TContainer>(document.Text);
            if (!parsed.AuthoritativeSuccess)
            {
                var failedProv = new AssetProvenance(root.Id, root.Name, filePath, relativePath, root.IsWritable, 0, -1, []);
                state.AllAssets.Add(new IndexedAsset(
                    kind,
                    string.Empty,
                    Path.GetFileNameWithoutExtension(filePath),
                    failedProv,
                    AuthoritativeDefinition: null,
                    StrictDefinition: null,
                    AuthoritativeParseFailed: true,
                    AuthoritativeParseError: parsed.AuthoritativeError,
                    StrictParseError: parsed.StrictError));
                continue;
            }

            List<TItem>? authItems = parsed.AuthoritativeValue != null ? getItems(parsed.AuthoritativeValue) : null;
            List<TItem>? strictItems = parsed.StrictSuccess && parsed.StrictValue != null ? getItems(parsed.StrictValue) : null;
            if (authItems == null)
            {
                continue;
            }

            for (int e = 0; e < authItems.Count; e++)
            {
                TItem authDef = authItems[e];
                object? strictDef = strictItems != null && e < strictItems.Count ? strictItems[e] : null;
                (string id, string displayName) = getIdentity(authDef);
                int tableIndex = e < entryTables.Count ? entryTables[e].Index : -1;

                RegisterIndexedAsset(
                    state,
                    in root,
                    kind,
                    id,
                    displayName,
                    filePath,
                    relativePath,
                    e,
                    tableIndex,
                    authDef,
                    strictDef,
                    authoritativeFailed: false,
                    authoritativeError: null,
                    strictError: parsed.StrictSuccess ? null : parsed.StrictError);
            }
        }
    }

    private static void IndexMaterialDirectory(in WorkspaceRoot root, IndexBuilderState state)
    {
        string directory = Path.Combine(root.AssetsDirectory, "materials");
        if (!Directory.Exists(directory))
        {
            return;
        }

        string[] files = Directory.GetFiles(directory, "*.toml", SearchOption.AllDirectories);
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);

        for (int f = 0; f < files.Length; f++)
        {
            string filePath = Path.GetFullPath(files[f]);
            TomlDocument document = LoadAndTrackDocument(filePath, state);
            string relativeToMaterials = NormalizeRelativePath(directory, filePath);
            string id = StripExtension(relativeToMaterials);
            string relativePath = NormalizeRelativePath(root.AssetsDirectory, filePath);

            if (document.HasSyntaxErrors)
            {
                continue;
            }

            DualTomlParseResult<MaterialDefinition> parsed = DualTomlParser.Parse<MaterialDefinition>(document.Text);
            RegisterIndexedAsset(
                state,
                in root,
                AssetKind.Material,
                id,
                id,
                filePath,
                relativePath,
                entryIndex: 0,
                tableIndex: -1,
                parsed.AuthoritativeSuccess ? parsed.AuthoritativeValue : null,
                parsed.StrictSuccess ? parsed.StrictValue : null,
                authoritativeFailed: !parsed.AuthoritativeSuccess,
                authoritativeError: parsed.AuthoritativeError,
                strictError: parsed.StrictSuccess ? null : parsed.StrictError);
        }
    }

    private static void RegisterIndexedAsset(
        IndexBuilderState state,
        in WorkspaceRoot root,
        AssetKind kind,
        string id,
        string displayName,
        string filePath,
        string relativePath,
        int entryIndex,
        int tableIndex,
        object? authDef,
        object? strictDef,
        bool authoritativeFailed,
        string? authoritativeError,
        string? strictError)
    {
        List<AssetShadowEntry> shadowed = [];
        if (!string.IsNullOrWhiteSpace(id) && state.WinningAssets.TryGetValue((kind, id), out IndexedAsset previous))
        {
            shadowed.AddRange(previous.Provenance.ShadowedProviders);
            shadowed.Add(new AssetShadowEntry(
                previous.Provenance.ProviderRootId,
                previous.Provenance.ProviderRootName,
                previous.Provenance.FilePath,
                previous.Provenance.RelativePath,
                previous.Provenance.IsWritable,
                previous.Provenance.EntryIndex,
                previous.Provenance.TableIndex));
        }

        var provenance = new AssetProvenance(
            root.Id,
            root.Name,
            filePath,
            relativePath,
            root.IsWritable,
            entryIndex,
            tableIndex,
            shadowed);

        var indexed = new IndexedAsset(
            kind,
            id,
            displayName,
            provenance,
            authDef,
            strictDef,
            authoritativeFailed,
            authoritativeError,
            strictError);

        state.AllAssets.Add(indexed);
        if (!string.IsNullOrWhiteSpace(id))
        {
            state.WinningAssets[(kind, id)] = indexed;
        }
    }

    private static void IndexRootResources(in WorkspaceRoot root, IndexBuilderState state)
    {
        IndexResourceDirectory(in root, "textures", ResourceKind.Texture, TEXTURE_EXTENSIONS, dropExtension: false, state);
        IndexResourceDirectory(in root, "meshes", ResourceKind.Mesh, [".obj"], dropExtension: false, state);
        IndexResourceDirectory(in root, "audio", ResourceKind.Audio, AUDIO_EXTENSIONS, dropExtension: false, state);
        IndexResourceDirectory(in root, "shaders", ResourceKind.Shader, SHADER_EXTENSIONS, dropExtension: true, state);

        IndexBlockAlbedoLayers(in root, state);
    }

    private static void IndexResourceDirectory(
        in WorkspaceRoot root,
        string subDirectory,
        ResourceKind kind,
        string[] allowedExtensions,
        bool dropExtension,
        IndexBuilderState state)
    {
        string directory = Path.Combine(root.AssetsDirectory, subDirectory);
        if (!Directory.Exists(directory))
        {
            return;
        }

        string[] files = Directory.GetFiles(directory, "*", SearchOption.AllDirectories);
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < files.Length; i++)
        {
            string filePath = Path.GetFullPath(files[i]);
            if (!HasAllowedExtension(filePath, allowedExtensions))
            {
                continue;
            }

            string relative = NormalizeRelativePath(directory, filePath);
            string id = dropExtension ? StripExtension(relative) : relative;

            List<string> shadowed = [];
            if (state.WinningResources.TryGetValue((kind, id), out IndexedResource previous))
            {
                shadowed.AddRange(previous.ShadowedRootIds);
                shadowed.Add(previous.ProviderRootId);
            }

            var resource = new IndexedResource(kind, id, root.Id, root.Name, filePath, relative, root.IsWritable, shadowed);
            state.AllResources.Add(resource);
            state.WinningResources[(kind, id)] = resource;
        }
    }

    private static void IndexBlockAlbedoLayers(in WorkspaceRoot root, IndexBuilderState state)
    {
        string blockDirectory = Path.Combine(root.AssetsDirectory, "textures", "block");
        if (!Directory.Exists(blockDirectory))
        {
            return;
        }

        string[] files = Directory.GetFiles(blockDirectory, "*", SearchOption.AllDirectories);
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < files.Length; i++)
        {
            string filePath = Path.GetFullPath(files[i]);
            if (!HasAllowedExtension(filePath, TEXTURE_EXTENSIONS))
            {
                continue;
            }

            string fileName = Path.GetFileName(filePath);
            if (IsNonAlbedoPbrSuffix(fileName))
            {
                continue;
            }

            string layerName = Path.GetFileNameWithoutExtension(fileName);
            string relative = NormalizeRelativePath(Path.Combine(root.AssetsDirectory, "textures"), filePath);

            List<string> shadowed = [];
            if (state.WinningBlockAlbedoLayers.TryGetValue(layerName, out IndexedResource previous))
            {
                shadowed.AddRange(previous.ShadowedRootIds);
                shadowed.Add(previous.ProviderRootId);
            }

            state.WinningBlockAlbedoLayers[layerName] = new IndexedResource(
                ResourceKind.Texture,
                layerName,
                root.Id,
                root.Name,
                filePath,
                relative,
                root.IsWritable,
                shadowed);
        }
    }

    private static void IndexRootLocalizationAndTags(in WorkspaceRoot root, IndexBuilderState state)
    {
        string langDirectory = Path.Combine(root.AssetsDirectory, "lang");
        if (!Directory.Exists(langDirectory))
        {
            return;
        }

        string[] files = Directory.GetFiles(langDirectory, "*.toml", SearchOption.AllDirectories);
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < files.Length; i++)
        {
            string filePath = Path.GetFullPath(files[i]);
            TomlDocument document = LoadAndTrackDocument(filePath, state);
            if (document.HasSyntaxErrors)
            {
                continue;
            }

            string relative = NormalizeRelativePath(langDirectory, filePath);
            string locale = ReadRootString(document, "Language");
            bool isTagPath = relative.StartsWith("tags/", StringComparison.OrdinalIgnoreCase) ||
                             relative.Contains("/tags/", StringComparison.OrdinalIgnoreCase);

            if (isTagPath)
            {
                IndexTagDocument(in root, filePath, locale, document, state);
            }
            else
            {
                IndexLocalizationDocument(in root, filePath, locale, document, state);
            }
        }
    }

    private static void IndexLocalizationDocument(
        in WorkspaceRoot root,
        string filePath,
        string locale,
        TomlDocument document,
        IndexBuilderState state)
    {
        Result<TomlTableSpan> translationsTable = document.GetTable("Translations");
        if (!translationsTable.Success)
        {
            return;
        }

        IReadOnlyList<TomlFieldSpan> fields = translationsTable.Value.Fields;
        for (int i = 0; i < fields.Count; i++)
        {
            TomlFieldSpan field = fields[i];
            string value = field.TypedValue as string ?? string.Empty;
            state.LocalizationEntries.Add(new IndexedLocalizationEntry(
                locale,
                field.Key,
                value,
                root.Id,
                root.Name,
                filePath,
                root.IsWritable,
                field));
        }
    }

    private static void IndexTagDocument(
        in WorkspaceRoot root,
        string filePath,
        string locale,
        TomlDocument document,
        IndexBuilderState state)
    {
        Result<TomlTableSpan> tagsTable = document.GetTable("Tags");
        if (!tagsTable.Success)
        {
            return;
        }

        IReadOnlyList<TomlFieldSpan> fields = tagsTable.Value.Fields;
        for (int i = 0; i < fields.Count; i++)
        {
            TomlFieldSpan field = fields[i];
            List<string> values = [];
            for (int j = 0; j < field.ArrayElements.Count; j++)
            {
                if (field.ArrayElements[j].TypedValue is string item)
                {
                    values.Add(item);
                }
            }

            state.TagEntries.Add(new IndexedTagEntry(
                locale,
                field.Key,
                values,
                root.Id,
                root.Name,
                filePath,
                root.IsWritable,
                field));
        }
    }

    private static void IndexRootBlueprints(in WorkspaceRoot root, IndexBuilderState state)
    {
        string blueprintDirectory = Path.Combine(root.AssetsDirectory, "blueprints");
        if (!Directory.Exists(blueprintDirectory))
        {
            return;
        }

        string[] files = Directory.GetFiles(blueprintDirectory, "*.wbp", SearchOption.AllDirectories);
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < files.Length; i++)
        {
            state.BlueprintFiles.Add(Path.GetFullPath(files[i]));
        }
    }

    private static void ExtractReferences(IndexBuilderState state)
    {
        foreach (IndexedAsset asset in state.WinningAssets.Values)
        {
            if (asset.AuthoritativeDefinition == null)
            {
                continue;
            }

            switch (asset.AuthoritativeDefinition)
            {
                case ItemDefinition item:
                    ExtractItemReferences(in asset, in item, state.AllReferences);
                    break;

                case BrickDefinition brick:
                    ExtractBrickReferences(in asset, in brick, state.AllReferences);
                    break;

                case PaintDefinition paint:
                    ExtractPaintReferences(in asset, in paint, state.AllReferences);
                    break;

                case SkillDefinition skill:
                    ExtractSkillReferences(in asset, in skill, state.AllReferences);
                    break;

                case BodyDefinition body:
                    ExtractBodyReferences(in asset, in body, state.AllReferences);
                    break;

                case RecipeDefinition recipe:
                    ExtractRecipeReferences(in asset, in recipe, state.AllReferences);
                    break;

                case MaterialDefinition material:
                    ExtractMaterialReferences(in asset, in material, state.AllReferences);
                    break;
            }
        }

        for (int i = 0; i < state.TagEntries.Count; i++)
        {
            IndexedTagEntry tag = state.TagEntries[i];
            string grammar = InferTagValueGrammar(tag.FilePath, tag.TagName);
            for (int j = 0; j < tag.Values.Count; j++)
            {
                state.AllReferences.Add(new InboundReference(
                    AssetKind.Tag,
                    tag.TagName,
                    tag.FilePath,
                    tag.TagName,
                    tag.Values[j],
                    grammar,
                    IsInformational: false,
                    tag.FieldSpan.KeySpan.Line));
            }
        }
    }

    private static void ExtractItemReferences(in IndexedAsset asset, in ItemDefinition item, List<InboundReference> refs)
    {
        AddRefIfPresent(refs, in asset, "Name", item.Name, "localization", isInformational: false);
        AddRefIfPresent(refs, in asset, "Description", item.Description, "localization", isInformational: false);
        AddRefIfPresent(refs, in asset, "Icon", item.Icon, "texture", isInformational: false);

        if (item.Placeable.HasValue)
        {
            if (item.Placeable.Value.Type == PlaceableType.Brick)
            {
                AddRefIfPresent(refs, in asset, "Placeable.ID", item.Placeable.Value.ID, "brick", isInformational: false);
            }
            else if (item.Placeable.Value.Type == PlaceableType.Blueprint)
            {
                AddRefIfPresent(refs, in asset, "Placeable.ID", item.Placeable.Value.ID, "blueprint", isInformational: false);
            }
        }

        if (item.ViewModel.HasValue)
        {
            AddRefIfPresent(refs, in asset, "ViewModel.Mesh", item.ViewModel.Value.Mesh, "mesh", isInformational: false);
            AddRefIfPresent(refs, in asset, "ViewModel.Material", item.ViewModel.Value.Material, "material", isInformational: false);
        }

        if (item.WorldModel.HasValue)
        {
            AddRefIfPresent(refs, in asset, "WorldModel.Mesh", item.WorldModel.Value.Mesh, "mesh", isInformational: true);
            AddRefIfPresent(refs, in asset, "WorldModel.Material", item.WorldModel.Value.Material, "material", isInformational: true);
        }
    }

    private static void ExtractBrickReferences(in IndexedAsset asset, in BrickDefinition brick, List<InboundReference> refs)
    {
        AddRefIfPresent(refs, in asset, "Mesh", brick.Mesh, "mesh", isInformational: false);
        AddFaceRefs(refs, in asset, "Textures.Default", brick.Textures.Default);
        AddFaceRefs(refs, in asset, "Textures.Top", brick.Textures.Top);
        AddFaceRefs(refs, in asset, "Textures.Bottom", brick.Textures.Bottom);
        AddFaceRefs(refs, in asset, "Textures.Front", brick.Textures.Front);
        AddFaceRefs(refs, in asset, "Textures.Back", brick.Textures.Back);
        AddFaceRefs(refs, in asset, "Textures.Left", brick.Textures.Left);
        AddFaceRefs(refs, in asset, "Textures.Right", brick.Textures.Right);

        if (brick.Sounds != null)
        {
            foreach (KeyValuePair<BrickEventKind, string[]> kvp in brick.Sounds)
            {
                if (kvp.Value == null)
                {
                    continue;
                }

                for (int i = 0; i < kvp.Value.Length; i++)
                {
                    AddRefIfPresent(refs, in asset, $"Sounds.{kvp.Key}", kvp.Value[i], "sound_tag", isInformational: false);
                }
            }
        }
    }

    private static void ExtractPaintReferences(in IndexedAsset asset, in PaintDefinition paint, List<InboundReference> refs)
    {
        AddRefIfPresent(refs, in asset, "Name", paint.Name, "localization", isInformational: false);
        AddFaceRefs(refs, in asset, "Textures", paint.Textures);
    }

    private static void ExtractSkillReferences(in IndexedAsset asset, in SkillDefinition skill, List<InboundReference> refs)
    {
        AddRefIfPresent(refs, in asset, "Name", skill.Name, "localization", isInformational: false);
        AddRefIfPresent(refs, in asset, "Category", skill.Category, "localization", isInformational: true);
        AddRefIfPresent(refs, in asset, "Icon", skill.Icon, "texture", isInformational: true);

        if (skill.Sources != null)
        {
            foreach (KeyValuePair<XPSource, Dictionary<string, int>> sourceGroup in skill.Sources)
            {
                if (sourceGroup.Value == null)
                {
                    continue;
                }

                foreach (string target in sourceGroup.Value.Keys)
                {
                    if (sourceGroup.Key is XPSource.Craft or XPSource.Process)
                    {
                        if (target.StartsWith("station:", StringComparison.Ordinal))
                        {
                            AddRefIfPresent(refs, in asset, $"Sources.{sourceGroup.Key}", target.Substring(8), "station_tag", isInformational: false);
                        }
                        else if (target.StartsWith("recipe:", StringComparison.Ordinal))
                        {
                            AddRefIfPresent(refs, in asset, $"Sources.{sourceGroup.Key}", target.Substring(7), "recipe", isInformational: false);
                        }
                        else if (target.StartsWith("tag:", StringComparison.Ordinal))
                        {
                            AddRefIfPresent(refs, in asset, $"Sources.{sourceGroup.Key}", target.Substring(4), "tag", isInformational: false);
                        }
                        else
                        {
                            AddRefIfPresent(refs, in asset, $"Sources.{sourceGroup.Key}", target, "item", isInformational: false);
                        }

                        continue;
                    }

                    bool isTag = target.StartsWith("tag:", StringComparison.Ordinal);
                    string targetId = isTag ? target.Substring(4) : target;
                    string grammar = isTag ? "tag" : "brick";
                    AddRefIfPresent(refs, in asset, $"Sources.{sourceGroup.Key}", targetId, grammar, isInformational: false);
                }
            }
        }
    }

    private static void ExtractBodyReferences(in IndexedAsset asset, in BodyDefinition body, List<InboundReference> refs)
    {
        if (body.States == null)
        {
            return;
        }

        foreach (KeyValuePair<string, Dictionary<string, string?[]>> statePair in body.States)
        {
            if (statePair.Value == null)
            {
                continue;
            }

            foreach (KeyValuePair<string, string?[]> dirPair in statePair.Value)
            {
                if (dirPair.Value == null)
                {
                    continue;
                }

                for (int i = 0; i < dirPair.Value.Length; i++)
                {
                    AddRefIfPresent(refs, in asset, $"States.{statePair.Key}.{dirPair.Key}", dirPair.Value[i], "texture", isInformational: false);
                }
            }
        }
    }

    private static void ExtractRecipeReferences(in IndexedAsset asset, in RecipeDefinition recipe, List<InboundReference> refs)
    {
        AddRefIfPresent(refs, in asset, "TargetTag", recipe.TargetTag, "target_tag", isInformational: false);
        AddRefIfPresent(refs, in asset, "Category", recipe.Category, "localization", isInformational: true);

        if (recipe.Inputs != null)
        {
            for (int i = 0; i < recipe.Inputs.Count; i++)
            {
                string? rawItemId = recipe.Inputs[i].ItemId;
                if (string.IsNullOrWhiteSpace(rawItemId))
                {
                    continue;
                }

                string trimmed = rawItemId.Trim();
                if (trimmed.StartsWith("tag:", StringComparison.Ordinal))
                {
                    AddRefIfPresent(refs, in asset, $"Inputs[{i}].ItemId", trimmed.Substring(4), "tag", isInformational: false);
                }
                else
                {
                    AddRefIfPresent(refs, in asset, $"Inputs[{i}].ItemId", trimmed, "item", isInformational: false);
                }
            }
        }

        if (recipe.Outputs != null)
        {
            for (int i = 0; i < recipe.Outputs.Count; i++)
            {
                AddRefIfPresent(refs, in asset, $"Outputs[{i}].ItemId", recipe.Outputs[i].ItemId?.Trim(), "item", isInformational: false);
            }
        }
    }

    private static void ExtractMaterialReferences(in IndexedAsset asset, in MaterialDefinition material, List<InboundReference> refs)
    {
        AddRefIfPresent(refs, in asset, "Shader", material.Shader, "shader", isInformational: false);
        if (material.Textures != null)
        {
            for (int i = 0; i < material.Textures.Length; i++)
            {
                AddRefIfPresent(refs, in asset, "Textures", material.Textures[i], "texture", isInformational: false);
            }
        }
    }

    private static void AddFaceRefs(List<InboundReference> refs, in IndexedAsset asset, string fieldName, string?[]? layers)
    {
        if (layers == null)
        {
            return;
        }

        for (int i = 0; i < layers.Length; i++)
        {
            AddRefIfPresent(refs, in asset, fieldName, layers[i], "brick_face", isInformational: false);
        }
    }

    private static void AddRefIfPresent(
        List<InboundReference> refs,
        in IndexedAsset asset,
        string fieldName,
        string? targetId,
        string grammar,
        bool isInformational)
    {
        if (string.IsNullOrWhiteSpace(targetId))
        {
            return;
        }

        refs.Add(new InboundReference(
            asset.Kind,
            asset.Id,
            asset.Provenance.FilePath,
            fieldName,
            targetId,
            grammar,
            isInformational,
            Line: 1));
    }

    private static string InferTagValueGrammar(string filePath, string tagName)
    {
        string fileName = Path.GetFileName(filePath);
        if (string.Equals(fileName, "sounds.toml", StringComparison.OrdinalIgnoreCase))
        {
            return "audio";
        }

        if (tagName.StartsWith("brick.", StringComparison.Ordinal) ||
            string.Equals(fileName, "bricks.toml", StringComparison.OrdinalIgnoreCase))
        {
            return "brick";
        }

        if (tagName.StartsWith("paint.", StringComparison.Ordinal) ||
            string.Equals(fileName, "paints.toml", StringComparison.OrdinalIgnoreCase))
        {
            return "paint";
        }

        if (tagName.StartsWith("item.", StringComparison.Ordinal) ||
            string.Equals(fileName, "items.toml", StringComparison.OrdinalIgnoreCase))
        {
            return "item";
        }

        return "tag_value";
    }

    private static TomlDocument LoadAndTrackDocument(string filePath, IndexBuilderState state)
    {
        if (state.DocumentsByPath.TryGetValue(filePath, out TomlDocument? existing))
        {
            return existing;
        }

        byte[] bytes = File.ReadAllBytes(filePath);
        TomlDocument document = TomlDocument.Parse(bytes, filePath);
        state.DocumentsByPath[filePath] = document;
        return document;
    }

    private static string ReadRootString(TomlDocument doc, string key)
    {
        Result<TomlFieldSpan> field = doc.GetRootField(key);
        return field.Success && field.Value.TypedValue is string value ? value : string.Empty;
    }

    private static string? ReadOptionalRootString(TomlDocument doc, string key)
    {
        Result<TomlFieldSpan> field = doc.GetRootField(key);
        return field.Success && field.Value.TypedValue is string value && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;
    }

    private static IReadOnlyList<string> ReadRootStringArray(TomlDocument doc, string key)
    {
        Result<TomlFieldSpan> field = doc.GetRootField(key);
        if (!field.Success || field.Value.ValueKind != TomlValueKind.Array)
        {
            return [];
        }

        List<string> values = new(field.Value.ArrayElements.Count);
        for (int i = 0; i < field.Value.ArrayElements.Count; i++)
        {
            if (field.Value.ArrayElements[i].TypedValue is string item)
            {
                values.Add(item);
            }
        }

        return values;
    }

    private static string NormalizeRelativePath(string rootDirectory, string fullPath)
    {
        return Path.GetRelativePath(rootDirectory, fullPath).Replace('\\', '/');
    }

    private static string StripExtension(string relativePath)
    {
        int dotIndex = relativePath.LastIndexOf('.');
        int slashIndex = relativePath.LastIndexOf('/');
        return dotIndex > slashIndex ? relativePath.Substring(0, dotIndex) : relativePath;
    }

    private static bool HasAllowedExtension(string filePath, string[] allowedExtensions)
    {
        for (int i = 0; i < allowedExtensions.Length; i++)
        {
            if (filePath.EndsWith(allowedExtensions[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsNonAlbedoPbrSuffix(string fileName)
    {
        for (int i = 0; i < NON_ALBEDO_SUFFIXES.Length; i++)
        {
            if (fileName.EndsWith(NON_ALBEDO_SUFFIXES[i], StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private sealed class IndexBuilderState
    {
        public List<IndexedAsset> AllAssets { get; } = [];
        public Dictionary<(AssetKind Kind, string Id), IndexedAsset> WinningAssets { get; } = new();
        public List<IndexedResource> AllResources { get; } = [];
        public Dictionary<(ResourceKind Kind, string Id), IndexedResource> WinningResources { get; } = new();
        public Dictionary<string, IndexedResource> WinningBlockAlbedoLayers { get; } = new(StringComparer.Ordinal);
        public List<IndexedLocalizationEntry> LocalizationEntries { get; } = [];
        public List<IndexedTagEntry> TagEntries { get; } = [];
        public Dictionary<string, TomlDocument> DocumentsByPath { get; } = new(StringComparer.Ordinal);
        public List<InboundReference> AllReferences { get; } = [];
        public List<string> BlueprintFiles { get; } = [];
    }
}

using Shoal.Modularity;
using Swordfish.Library.Diagnostics;

namespace Shoal.DependencyInjection;

/// <summary>Discovers module manifests, resolves merged module options, and loads language files.</summary>
internal class ConfigurationProvider
{
    private readonly ILogger _logger;
    private readonly IFileParseService _fileParseService;
    private readonly VirtualFileSystem _vfs;
    private readonly DiscoveredModule[] _discoveredModules;
    private readonly ParsedFile<ModuleOptions>? _modOptions;
    private ParsedFile<Language>[]? _languages;

    /// <summary>Discovers module manifests across configured search roots and resolves the effective module options.</summary>
    public ConfigurationProvider(
        ILogger logger,
        IFileParseService fileParseService,
        VirtualFileSystem vfs,
        AppPaths appPaths,
        ModuleResolutionOptions moduleResolution)
    {
        _logger = logger;
        _fileParseService = fileParseService;
        _vfs = vfs;

        IReadOnlyList<ModuleSearchRoot> searchRoots = moduleResolution.GetSearchRoots(in appPaths);
        _discoveredModules = LoadManifests(logger, fileParseService, searchRoots);
        logger.LogInformation("Found {count} module manifests.", _discoveredModules.Length);
        if (_discoveredModules.Length == 0)
        {
            throw new FatalAlertException("At least one module manifest must be present.");
        }

        _modOptions = LoadModuleOptions(logger, fileParseService, appPaths, moduleResolution);
    }

    /// <summary>Gets all language definitions discovered under the virtual file system.</summary>
    public IReadOnlyCollection<ParsedFile<Language>> GetLanguages()
    {
        if (_languages != null)
        {
            return _languages;
        }

        //  This isn't ideal. Languages should be loaded during construction but the VFS hasn't
        //  been mounted until after this class is constructed by ModulesLoader's dependency on it.
        _languages = LoadLanguages(_fileParseService, _vfs);
        _logger.LogInformation("Loaded {count} language definitions.", _languages.Length);
        return _languages;
    }

    /// <summary>Gets the effective module options merged from install and per-user configuration.</summary>
    public ParsedFile<ModuleOptions>? GetModuleOptions()
    {
        return _modOptions;
    }

    /// <summary>Gets the discovered modules paired with the policy of their owning search root.</summary>
    public IReadOnlyCollection<DiscoveredModule> GetDiscoveredModules()
    {
        return _discoveredModules;
    }

    private static ParsedFile<ModuleOptions>? LoadModuleOptions(
        ILogger logger,
        IFileParseService fileParseService,
        AppPaths appPaths,
        ModuleResolutionOptions moduleResolution)
    {
        PathInfo primaryPath = moduleResolution.PrimaryConfigPath;
        ModuleOptions? installOptions = null;
        if (fileParseService.TryParse(primaryPath, out ModuleOptions parsedInstall))
        {
            installOptions = parsedInstall;
            logger.LogInformation("Found module options at '{path}'.", primaryPath.Value);
        }
        else
        {
            logger.LogInformation("No module options found at '{path}'.", primaryPath.Value);
        }

        PathInfo? userConfigPath = moduleResolution.GetUserConfigPath(in appPaths);
        ModuleOptions? userOptions = null;
        if (userConfigPath.HasValue && userConfigPath.Value.FileExists())
        {
            if (fileParseService.TryParse(userConfigPath.Value, out ModuleOptions parsedUser))
            {
                userOptions = parsedUser;
                logger.LogInformation("Found user module options at '{path}'.", userConfigPath.Value.Value);
            }
            else
            {
                logger.LogWarning("Failed to parse user module options at '{path}'. Using install module options only.", userConfigPath.Value.Value);
            }
        }

        ModuleOptions? merged = ModuleOptions.Merge(installOptions, userOptions);
        if (merged == null)
        {
            return null;
        }

        PathInfo sourcePath = installOptions != null ? primaryPath : userConfigPath!.Value;
        return new ParsedFile<ModuleOptions>(sourcePath, merged);
    }

    private static ParsedFile<Language>[] LoadLanguages(IFileParseService fileParseService, VirtualFileSystem vfs)
    {
        IEnumerable<PathInfo> enumerableTomlFiles = vfs.GetFiles(VirtualPaths.Lang, SearchOption.AllDirectories)
            .Where(file => file.GetExtension() == ".toml");

        var languages = new List<ParsedFile<Language>>();
        foreach (PathInfo file in enumerableTomlFiles)
        {
            if (fileParseService.TryParse(file, out Language language))
            {
                languages.Add(new ParsedFile<Language>(file, language));
            }
        }

        return [.. languages];
    }

    private static DiscoveredModule[] LoadManifests(
        ILogger logger,
        IFileParseService fileParseService,
        IReadOnlyList<ModuleSearchRoot> searchRoots)
    {
        List<DiscoveredModule> manifests = [];
        Dictionary<string, PathInfo> discoveredById = new(StringComparer.Ordinal);

        for (int rootIndex = 0; rootIndex < searchRoots.Count; rootIndex++)
        {
            ModuleSearchRoot root = searchRoots[rootIndex];
            PathInfo[] files = root.Path.GetFiles("manifest.toml", SearchOption.AllDirectories);
            if (files.Length == 0)
            {
                continue;
            }

            Array.Sort(files, ComparePathsDeterministically);

            for (int i = 0; i < files.Length; i++)
            {
                PathInfo file = files[i];
                if (!fileParseService.TryParse(file, out ModuleManifest manifest))
                {
                    logger.LogWarning("Failed to parse module manifest at '{path}'.", file.Value);
                    continue;
                }

                if (root.Policy == ModuleRootPolicy.ContentOnly && ModulesLoader.HasAssemblies(manifest))
                {
                    logger.LogError(
                        "Rejected module \"{name}\" ({id}) at '{path}': content-only root '{root}' does not allow assemblies.",
                        manifest.Name,
                        manifest.ID,
                        file.Value,
                        root.Path.Value);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(manifest.ID))
                {
                    logger.LogWarning("Ignoring module manifest at '{path}' because its ID is missing.", file.Value);
                    continue;
                }

                if (discoveredById.TryGetValue(manifest.ID, out PathInfo existingPath))
                {
                    logger.LogWarning(
                        "Ignoring duplicate module ID \"{id}\" at '{path}'; already discovered at '{existingPath}'.",
                        manifest.ID,
                        file.Value,
                        existingPath.Value);
                    continue;
                }

                discoveredById.Add(manifest.ID, file);
                manifests.Add(new DiscoveredModule(new ParsedFile<ModuleManifest>(file, manifest), root.Policy));
            }
        }

        return [.. manifests];
    }

    private static int ComparePathsDeterministically(PathInfo left, PathInfo right)
    {
        int insensitive = StringComparer.OrdinalIgnoreCase.Compare(left.Value, right.Value);
        return insensitive != 0 ? insensitive : StringComparer.Ordinal.Compare(left.Value, right.Value);
    }
}

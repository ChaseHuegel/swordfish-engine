using Shoal.Modularity;
using Swordfish.Library.Diagnostics;

namespace Shoal.DependencyInjection;

internal class ConfigurationProvider
{
    private const string FILE_MODULE_OPTIONS = "modules.toml";

    private readonly ParsedFile<Language>[] _languages;
    private readonly ParsedFile<ModuleManifest>[] _modManifests;
    private readonly ParsedFile<ModuleOptions>? _modOptions;

    public ConfigurationProvider(ILogger logger, IFileParseService fileParseService, VirtualFileSystem vfs)
    {
        _languages = LoadLanguages(fileParseService, vfs);
        logger.LogInformation("Found {count} languages.", _languages.Length);

        _modManifests = LoadManifests(fileParseService);
        logger.LogInformation("Found {count} module manifests.", _modManifests.Length);
        if (_modManifests.Length == 0)
        {
            throw new FatalAlertException("At least one module manifest must be present.");
        }

        PathInfo modOptionsPath = Paths.Config.At(FILE_MODULE_OPTIONS);
        if (fileParseService.TryParse(modOptionsPath, out ModuleOptions modOptions))
        {
            _modOptions = new ParsedFile<ModuleOptions>(modOptionsPath, modOptions);
            logger.LogInformation("Found module options.");
        }
        else
        {
            logger.LogInformation("No module options found.");
        }
    }

    public IReadOnlyCollection<ParsedFile<Language>> GetLanguages()
    {
        return _languages;
    }

    public ParsedFile<ModuleOptions>? GetModuleOptions()
    {
        return _modOptions;
    }

    public IReadOnlyCollection<ParsedFile<ModuleManifest>> GetModuleManifests()
    {
        return _modManifests;
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

    private static ParsedFile<ModuleManifest>[] LoadManifests(IFileParseService fileParseService)
    {
        PathInfo[] files = Paths.Modules.GetFiles("manifest.toml", SearchOption.AllDirectories);

        var manifests = new List<ParsedFile<ModuleManifest>>();
        for (var i = 0; i < files.Length; i++)
        {
            PathInfo file = files[i];
            if (fileParseService.TryParse(file, out ModuleManifest manifest))
            {
                manifests.Add(new ParsedFile<ModuleManifest>(file, manifest));
            }
        }

        return [.. manifests];
    }
}
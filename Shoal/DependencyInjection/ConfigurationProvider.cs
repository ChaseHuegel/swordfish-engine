using Shoal.IO;
using Shoal.Modularity;
using Swordfish.Library.Diagnostics;

namespace Shoal.DependencyInjection;

internal class ConfigurationProvider
{
    private const string FILE_MODULE_OPTIONS = "modules.toml";

    private readonly ParsedFile<Language>[] _languages;
    private readonly ParsedFile<ModuleManifest>[] _modManifests;
    private readonly ParsedFile<ModuleOptions>? _modOptions;

    public ConfigurationProvider(ILogger logger, IFileService fileService)
    {
        _languages = LoadLanguages(fileService);
        logger.LogInformation("Found {count} languages.", _languages.Length);

        _modManifests = LoadManifests(fileService);
        logger.LogInformation("Found {count} module manifests.", _modManifests.Length);
        if (_modManifests.Length == 0)
        {
            throw new FatalAlertException("At least one module manifest must be present.");
        }

        PathInfo modOptionsPath = Paths.Config.At(FILE_MODULE_OPTIONS);
        if (fileService.TryParse(modOptionsPath, out ModuleOptions modOptions))
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

    private static ParsedFile<Language>[] LoadLanguages(IFileService fileService)
    {
        PathInfo[] langFiles = fileService.GetFiles(Paths.Lang, "*.toml", SearchOption.AllDirectories);

        var languages = new List<ParsedFile<Language>>();
        for (int i = 0; i < langFiles.Length; i++)
        {
            PathInfo path = langFiles[i];
            if (fileService.TryParse(path, out Language language))
            {
                languages.Add(new ParsedFile<Language>(path, language));
            }
        }

        return [.. languages];
    }

    private static ParsedFile<ModuleManifest>[] LoadManifests(IFileService fileService)
    {
        PathInfo[] files = fileService.GetFiles(Paths.Modules, "manifest.toml", SearchOption.AllDirectories);

        var manifests = new List<ParsedFile<ModuleManifest>>();
        for (int i = 0; i < files.Length; i++)
        {
            PathInfo file = files[i];
            if (fileService.TryParse(file, out ModuleManifest manifest))
            {
                manifests.Add(new ParsedFile<ModuleManifest>(file, manifest));
            }
        }

        return [.. manifests];
    }
}
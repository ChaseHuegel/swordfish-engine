using Swordfish.Library.Util;

namespace Shoal.Modularity;

internal class ModulesLoader : IModulesLoader
{
    private readonly ILogger _logger;
    private readonly ModuleOptions? _options;
    private readonly IReadOnlyCollection<ParsedFile<ModuleManifest>> _manifests;
    private readonly ModuleCatalog? _catalog;

    public ModulesLoader(ILogger logger, ConfigurationProvider configurationProvider, ModuleCatalog catalog)
        : this(logger, configurationProvider.GetModuleOptions()?.Value, configurationProvider.GetModuleManifests(), catalog)
    {
    }

    internal ModulesLoader(
        ILogger logger,
        ModuleOptions? options,
        IReadOnlyCollection<ParsedFile<ModuleManifest>> manifests,
        ModuleCatalog? catalog = null
    )
    {
        _logger = logger;
        _options = options;
        _manifests = manifests;
        _catalog = catalog;
    }

    public void Load(Action<ParsedFile<ModuleManifest>, IReadOnlyList<Assembly>> moduleLoadedCallback)
    {
        if (_manifests.Count == 0)
        {
            _logger.LogInformation("No modules found to load.");
            return;
        }

        if (_options == null)
        {
            _logger.LogError("No module options were found, but there are {count} modules present. Modules will not be loaded.", _manifests.Count);
            return;
        }

        if (_options.LoadOrder == null || _options.LoadOrder.Length == 0)
        {
            _logger.LogError("Module load order is not specified, but there are {count} modules present. Modules will not be loaded.", _manifests.Count);
            return;
        }

        if (_options.LoadOrder.Length != _manifests.Count || !_manifests.All(manfiest => _options.LoadOrder.Contains(manfiest.Value.ID)))
        {
            _logger.LogWarning("Not all present modules are specified in the load order. Some modules will not be loaded.");
        }

        if (!_options.LoadOrder.All(id => _manifests.Select(manfiest => manfiest.Value.ID).Contains(id)))
        {
            _logger.LogWarning("Some modules specified in the load order are missing and will not be loaded.");
        }

        if (_options.AllowScriptCompilation)
        {
            _logger.LogInformation("Script compilation is enabled.");
        }
        else
        {
            _logger.LogInformation("Script compilation is disabled.");
        }

        foreach (string id in _options.LoadOrder)
        {
            ParsedFile<ModuleManifest>? manifestFile = _manifests.FirstOrDefault(manifest => manifest.Value.ID == id);
            if (manifestFile == null)
            {
                _logger.LogError("Tried to load module ID \"{id}\" from the load order but it could not be found. Is it missing a manifest?", id);
                continue;
            }

            ModuleManifest manifest = manifestFile.Value;
            Result<Exception?> result = LoadModule(moduleLoadedCallback, _logger, _options, manifestFile);
            if (result)
            {
                _catalog?.Add(new LoadedModule(
                    manifest.ID ?? string.Empty,
                    manifest.Name ?? string.Empty,
                    manifestFile.GetRootPath()));

                _logger.LogInformation("Loaded module \"{name}\" ({id}), by \"{author}\": {description}", manifest.Name, manifest.ID, manifest.Author, manifest.Description);
            }
            else
            {
                _logger.LogError(result.Value, "Failed to load module \"{name}\" ({id}), by \"{author}\". {message}.", manifest.Name, manifest.ID, manifest.Author, result.Message);
            }
        }
    }

    private static Result<Exception?> LoadModule(Action<ParsedFile<ModuleManifest>, IReadOnlyList<Assembly>> moduleLoadedCallback, ILogger logger, ModuleOptions options, ParsedFile<ModuleManifest> manifestFile)
    {
        ModuleManifest manifest = manifestFile.Value;
        PathInfo directory = manifestFile.GetRootPath();

        //  Compile any scripts into an assembly
        PathInfo scriptsPath = manifest.ScriptsPath ?? directory.At("Scripts/");
        if (scriptsPath.Exists())
        {
            PathInfo[]? scriptFiles = scriptsPath.GetFiles(SearchOption.AllDirectories);
            if (options.AllowScriptCompilation && scriptFiles.Length > 0)
            {
                //  TODO implement script compilation
                logger.LogError("Unable to compile scripts in module {name} ({id}), script compilation is not supported.", manifest.Name, manifest.ID);
            }
        }

        //  Load any external assemblies
        var loadedAssemblies = new List<Assembly>();
        if (manifest.Assemblies != null)
        {
            PathInfo assembliesPath = manifest.AssembliesPath ?? directory;

            foreach (string assemblyName in manifest.Assemblies)
            {
                PathInfo assemblyPath = assembliesPath.At(assemblyName);
                try
                {
                    Assembly assembly = Assembly.LoadFrom(assemblyPath.Value);
                    loadedAssemblies.Add(assembly);
                }
                catch (Exception ex)
                {
                    return new Result<Exception?>(false, ex, $"Error loading assembly: {assemblyName}");
                }
            }

            foreach (Assembly assembly in loadedAssemblies)
            {
                try
                {
                    HookAssembly(assembly);
                }
                catch (Exception ex)
                {
                    return new Result<Exception?>(false, ex, $"Error hooking into assembly: {assembly.FullName}");
                }
            }
        }

        //  Content-only modules have no assemblies, so notify the host even when the list is empty.
        try
        {
            moduleLoadedCallback.Invoke(manifestFile, loadedAssemblies);
        }
        catch (Exception ex)
        {
            return new Result<Exception?>(false, ex, "Error invoking the module loaded callback.");
        }

        return new Result<Exception?>(true, null);
    }

    // ReSharper disable once UnusedParameter.Local
    private static void HookAssembly(Assembly assembly)
    {
        //  TODO bind container, attach events, etc.
    }
}

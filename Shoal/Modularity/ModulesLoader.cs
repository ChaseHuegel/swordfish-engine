using Swordfish.Library.Util;

namespace Shoal.Modularity;

/// <summary>Loads discovered Shoal modules in configured load order while enforcing root policies.</summary>
internal class ModulesLoader : IModulesLoader
{
    private readonly ILogger _logger;
    private readonly ModuleOptions? _options;
    private readonly IReadOnlyCollection<DiscoveredModule> _modules;
    private readonly ModuleCatalog? _catalog;

    /// <summary>Initializes the loader from the active configuration provider and module catalog.</summary>
    public ModulesLoader(ILogger logger, ConfigurationProvider configurationProvider, ModuleCatalog catalog)
        : this(logger, configurationProvider.GetModuleOptions()?.Value, configurationProvider.GetDiscoveredModules(), catalog)
    {
    }

    /// <summary>Initializes the loader with full-policy manifests for testing.</summary>
    internal ModulesLoader(
        ILogger logger,
        ModuleOptions? options,
        IReadOnlyCollection<ParsedFile<ModuleManifest>> manifests,
        ModuleCatalog? catalog = null
    )
        : this(logger, options, WrapFullPolicy(manifests), catalog)
    {
    }

    /// <summary>Initializes the loader with policy-tagged discovered modules.</summary>
    internal ModulesLoader(
        ILogger logger,
        ModuleOptions? options,
        IReadOnlyCollection<DiscoveredModule> modules,
        ModuleCatalog? catalog = null
    )
    {
        _logger = logger;
        _options = options;
        _modules = modules;
        _catalog = catalog;
    }

    /// <inheritdoc/>
    public void Load(Action<ParsedFile<ModuleManifest>, IReadOnlyList<Assembly>> moduleLoadedCallback)
    {
        if (_modules.Count == 0)
        {
            _logger.LogInformation("No modules found to load.");
            return;
        }

        if (_options == null)
        {
            _logger.LogError("No module options were found, but there are {count} modules present. Modules will not be loaded.", _modules.Count);
            return;
        }

        if (_options.LoadOrder == null || _options.LoadOrder.Length == 0)
        {
            _logger.LogError("Module load order is not specified, but there are {count} modules present. Modules will not be loaded.", _modules.Count);
            return;
        }

        if (_options.LoadOrder.Length != _modules.Count || !_modules.All(module => _options.LoadOrder.Contains(module.Manifest.Value.ID)))
        {
            _logger.LogWarning("Not all present modules are specified in the load order. Some modules will not be loaded.");
        }

        if (!_options.LoadOrder.All(id => _modules.Select(module => module.Manifest.Value.ID).Contains(id)))
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
            if (!TryFindModule(id, out DiscoveredModule module))
            {
                _logger.LogError("Tried to load module ID \"{id}\" from the load order but it could not be found. Is it missing a manifest?", id);
                continue;
            }

            ModuleManifest manifest = module.Manifest.Value;
            Result<Exception?> result = LoadModule(moduleLoadedCallback, _logger, _options, in module);
            if (result)
            {
                _catalog?.Add(new LoadedModule(
                    manifest.ID ?? string.Empty,
                    manifest.Name ?? string.Empty,
                    module.Manifest.GetRootPath()));

                _logger.LogInformation("Loaded module \"{name}\" ({id}), by \"{author}\": {description}", manifest.Name, manifest.ID, manifest.Author, manifest.Description);
            }
            else
            {
                _logger.LogError(result.Value, "Failed to load module \"{name}\" ({id}), by \"{author}\". {message}.", manifest.Name, manifest.ID, manifest.Author, result.Message);
            }
        }
    }

    /// <summary>Returns whether a module manifest declares one or more managed assemblies.</summary>
    internal static bool HasAssemblies(ModuleManifest manifest)
    {
        return manifest.Assemblies != null && manifest.Assemblies.Length > 0;
    }

    private bool TryFindModule(string id, out DiscoveredModule found)
    {
        foreach (DiscoveredModule module in _modules)
        {
            if (module.Manifest.Value.ID == id)
            {
                found = module;
                return true;
            }
        }

        found = default;
        return false;
    }

    private static DiscoveredModule[] WrapFullPolicy(IReadOnlyCollection<ParsedFile<ModuleManifest>> manifests)
    {
        var wrapped = new DiscoveredModule[manifests.Count];
        int index = 0;
        foreach (ParsedFile<ModuleManifest> manifest in manifests)
        {
            wrapped[index++] = new DiscoveredModule(manifest, ModuleRootPolicy.Full);
        }

        return wrapped;
    }

    private static Result<Exception?> LoadModule(
        Action<ParsedFile<ModuleManifest>, IReadOnlyList<Assembly>> moduleLoadedCallback,
        ILogger logger,
        ModuleOptions options,
        in DiscoveredModule module)
    {
        ParsedFile<ModuleManifest> manifestFile = module.Manifest;
        ModuleManifest manifest = manifestFile.Value;
        PathInfo directory = manifestFile.GetRootPath();

        if (module.Policy == ModuleRootPolicy.ContentOnly && HasAssemblies(manifest))
        {
            return new Result<Exception?>(false, null, "Content-only module root does not allow assemblies");
        }

        if (module.Policy != ModuleRootPolicy.ContentOnly && options.AllowScriptCompilation)
        {
            //  Compile any scripts into an assembly
            PathInfo scriptsPath = manifest.ScriptsPath ?? directory.At("Scripts/");
            if (scriptsPath.Exists())
            {
                PathInfo[] scriptFiles = scriptsPath.GetFiles(SearchOption.AllDirectories);
                if (scriptFiles.Length > 0)
                {
                    logger.LogError("Unable to compile scripts in module {name} ({id}), script compilation is not supported.", manifest.Name, manifest.ID);
                }
            }
        }

        //  Load any external assemblies
        var loadedAssemblies = new List<Assembly>();
        if (module.Policy != ModuleRootPolicy.ContentOnly && manifest.Assemblies != null)
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
    }
}

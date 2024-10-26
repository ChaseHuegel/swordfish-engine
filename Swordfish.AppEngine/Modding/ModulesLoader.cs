using Swordfish.Library.Util;

namespace Swordfish.AppEngine.Modding;

internal class ModulesLoader(
    in ILogger logger,
    in IFileService fileService,
    in ConfigurationProvider configurationProvider
) : IModulesLoader
{
    private readonly ILogger _logger = logger;
    private readonly IFileService _fileService = fileService;
    private readonly ModuleOptions? _options = configurationProvider.GetModuleOptions()?.Value;
    private readonly IReadOnlyCollection<ParsedFile<ModuleManifest>> _manifests = configurationProvider.GetModuleManifests();

    public void Load(Action<Assembly> hookCallback)
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
            ParsedFile<ModuleManifest>? manifestFile = _manifests.FirstOrDefault(manfiest => manfiest.Value.ID == id);
            if (manifestFile == null)
            {
                _logger.LogError("Tried to load module ID \"{id}\" from the load order but it could not be found. Is it missing a manifest?", id);
                continue;
            }

            ModuleManifest manifest = manifestFile.Value;
            IPath modDirectory = manifestFile.Path.GetDirectory();

            Result<Exception?> result = LoadModule(hookCallback, _logger, _fileService, _options, manifest, modDirectory);
            if (result)
            {
                _logger.LogInformation("Loaded module \"{name}\" ({id}), by \"{author}\": {description}", manifest.Name, manifest.ID, manifest.Author, manifest.Description);
            }
            else
            {
                _logger.LogError(result.Value, "Failed to load module \"{name}\" ({id}), by \"{author}\". {message}.", manifest.Name, manifest.ID, manifest.Author, result.Message);
            }
        }
    }

    private static Result<Exception?> LoadModule(Action<Assembly> hookCallback, ILogger logger, IFileService fileService, ModuleOptions options, ModuleManifest manifest, IPath directory)
    {
        IPath rootPath = manifest.RootPathOverride ?? directory;

        //  Compile any scripts into an assembly
        IPath scriptsPath = manifest.ScriptsPath ?? new Path("Scripts/");
        scriptsPath = rootPath.At(scriptsPath);
        if (scriptsPath.Exists())
        {
            var scriptFiles = fileService.GetFiles(scriptsPath, SearchOption.AllDirectories);
            if (options.AllowScriptCompilation && scriptFiles.Length > 0)
            {
                //  TODO implement script compilation
                logger.LogError("Unable to compile scripts in module {name} ({id}), script compilation is not supported.", manifest.Name, manifest.ID);
            }
        }

        //  Load any external assemblies
        if (manifest.Assemblies == null || manifest.Assemblies.Length <= 0)
        {
            return new Result<Exception?>(true, null);
        }

        IPath assembliesPath = manifest.AssembliesPath ?? new Path("");

        var loadedAssemblies = new List<Assembly>();
        foreach (string assemblyName in manifest.Assemblies)
        {
            IPath assemblyPath = rootPath.At(assembliesPath).At(assemblyName);
            try
            {
                Assembly assembly = Assembly.LoadFrom(assemblyPath.ToString());
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
                hookCallback.Invoke(assembly);
            }
            catch (Exception ex)
            {
                return new Result<Exception?>(false, ex, $"Error hooking into assembly: {assembly.FullName}");
            }
        }

        return new Result<Exception?>(true, null);
    }

    private static void HookAssembly(Assembly assembly)
    {
        //  TODO bind container, attach events, etc.
    }
}

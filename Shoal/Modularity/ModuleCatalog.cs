namespace Shoal.Modularity;

/// <summary>Read-only view of the modules that the engine loaded this session, in load order.</summary>
public sealed class ModuleCatalog
{
    private readonly List<LoadedModule> _modules = [];

    /// <summary>Gets the loaded modules in load order.</summary>
    public IReadOnlyList<LoadedModule> Modules => _modules;

    /// <summary>Appends a loaded module in load order.</summary>
    internal void Add(in LoadedModule module)
    {
        _modules.Add(module);
    }
}

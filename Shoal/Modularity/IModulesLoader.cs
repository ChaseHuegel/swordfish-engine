namespace Shoal.Modularity;

internal interface IModulesLoader
{
    void Load(Action<ParsedFile<ModuleManifest>, IReadOnlyList<Assembly>> moduleLoadedCallback);
}
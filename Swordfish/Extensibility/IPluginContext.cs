using Swordfish.Library.IO;

namespace Swordfish.Extensibility;

public interface IPluginContext
{
    void Initialize();

    bool IsLoaded(IPlugin plugin);

    bool IsLoaded<TPlugin>() where TPlugin : IPlugin;

    bool IsLoaded(Type type);

    void Load(IPlugin plugin);

    void LoadFrom(IPath path, SearchOption searchOption = SearchOption.TopDirectoryOnly);

    void Unload(IPlugin plugin);

    void Unload(Type type);

    void Unload<TPlugin>() where TPlugin : IPlugin;

    void UnloadAll();
}

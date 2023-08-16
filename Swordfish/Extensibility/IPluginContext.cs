using System.Reflection;
using Swordfish.Library.IO;

namespace Swordfish.Extensibility;

public interface IPluginContext
{
    IEnumerable<Type> GetRegisteredTypes();

    void Activate(IEnumerable<IPlugin> plugins);

    void Register(params Assembly[] assemblies);

    void RegisterFrom(IPath path, SearchOption searchOption = SearchOption.TopDirectoryOnly);

    bool IsLoaded(IPlugin plugin);

    bool IsLoaded(Type type);

    bool IsLoaded<TPlugin>() where TPlugin : IPlugin;

    void Unload(IPlugin plugin);

    void Unload(Type type);

    void Unload<TPlugin>() where TPlugin : IPlugin;

    void UnloadAll();
}

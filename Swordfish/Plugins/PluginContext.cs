using System.Collections.Concurrent;
using System.Reflection;
using Ninject;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.IO;

namespace Swordfish.Plugins;

public class PluginContext : IPluginContext
{
    private const string DUPLICATE_ERROR = "Tried to load duplicate plugin";
    private const string LOADFROM_ERROR = "Unable to load plugins at path";
    private const string INITIALIZE_ERROR = "Error initializing plugin";
    private const string UNLOAD_ERROR = "Error unloading plugin";
    private const string LOAD_ERROR = "Error loading plugin";
    private const string LOAD_SUCCESS = "Loaded plugin";

    private readonly ConcurrentDictionary<IPlugin, Assembly> plugins = new();

    public bool IsLoaded(IPlugin plugin)
    {
        return IsLoaded(plugin.GetType());
    }

    public bool IsLoaded<TPlugin>() where TPlugin : IPlugin
    {
        return IsLoaded(typeof(TPlugin));
    }

    public bool IsLoaded(Type type)
    {
        return plugins.Keys.Any(p => p.GetType() == type);
    }

    public void Load(IPlugin plugin)
    {
        if (IsLoaded(plugin))
        {
            Debug.Log($"{DUPLICATE_ERROR} '{plugin}'", LogType.WARNING);
            return;
        }

        if (Debug.TryInvoke(plugin.Load, $"{LOAD_ERROR} '{plugin}'"))
        {
            plugins.TryAdd(plugin, plugin.GetType().Assembly);
            Debug.Log($"{LOAD_SUCCESS} '{plugin}'");

            //  Load kernel modules from the plugin
            SwordfishEngine.Kernel.Load(plugin.GetType().Assembly);

            Debug.TryInvoke(plugin.Initialize, $"{INITIALIZE_ERROR} '{plugin}'");
        }
    }

    public void LoadFrom(IPath path, SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        Debug.TryInvoke(() => LoadFromInternal(path, searchOption), $"{LOADFROM_ERROR} '{path}'");
    }

    public void Unload(IPlugin plugin)
    {
        Unload(plugin.GetType());
    }

    public void Unload<TPlugin>() where TPlugin : IPlugin
    {
        Unload(typeof(TPlugin));
    }

    public void Unload(Type type)
    {
        foreach (IPlugin p in plugins.Keys.Where(p => p.GetType() == type))
            UnloadInternal(p);
    }

    public void UnloadAll()
    {
        foreach (IPlugin p in plugins.Keys)
            UnloadInternal(p);
    }

    private void LoadFromInternal(IPath path, SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        string[] files = Directory.GetFiles(path.OriginalString ?? string.Empty, "*.dll", searchOption);

        if (files.Length == 0)
            return;

        IEnumerable<Assembly> loadedAssemblies = files.Select(file => Assembly.LoadFrom(file));
        IEnumerable<Type> loadedTypes = loadedAssemblies.SelectMany(assembly => assembly.GetTypes());
        List<IPlugin> loadedPlugins = new();

        foreach (Type type in loadedTypes)
        {
            if (!type.IsInterface && typeof(IPlugin).IsAssignableFrom(type))
            {
                if (IsLoaded(type))
                {
                    Debug.Log($"{DUPLICATE_ERROR} '{type}'", LogType.WARNING);
                    continue;
                }

                if (Activator.CreateInstance(type) is IPlugin plugin && Debug.TryInvoke(plugin.Load, $"{LOAD_ERROR} '{plugin}'"))
                {
                    loadedPlugins.Add(plugin);
                    plugins.TryAdd(plugin, type.Assembly);

                    //  Load kernel modules from the plugin
                    SwordfishEngine.Kernel.Load(plugin.GetType().Assembly);

                    Debug.Log($"{LOAD_SUCCESS} '{plugin}'");
                }
            }
        }

        foreach (IPlugin plugin in loadedPlugins)
            Debug.TryInvoke(plugin.Initialize, $"{INITIALIZE_ERROR} '{plugin}'");
    }

    private void UnloadInternal(IPlugin plugin)
    {
        Debug.TryInvoke(plugin.Unload, $"{UNLOAD_ERROR} '{plugin}'");
        plugins.TryRemove(plugin, out _);
    }
}

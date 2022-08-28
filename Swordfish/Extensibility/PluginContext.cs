using System.Collections.Concurrent;
using System.Reflection;
using Ninject;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.IO;

namespace Swordfish.Extensibility;

public class PluginContext : IPluginContext
{
    private const string DUPLICATE_ERROR = "Tried to load a duplicate";
    private const string LOADFROM_ERROR = "Unable to load extensions from";
    private const string INITIALIZE_ERROR = "Unable to initialize";
    private const string UNLOAD_ERROR = "Unable to unload";
    private const string LOAD_ERROR = "Unable to load";
    private const string LOAD_SUCCESS = "Loaded";

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
            Debugger.Log($"{DUPLICATE_ERROR} {GetSimpleTypeString(plugin)} '{plugin}' at '{plugin.GetType().Assembly.Location}'", LogType.WARNING);
            return;
        }

        if (Debugger.TryInvoke(plugin.Load, $"{LOAD_ERROR} {GetSimpleTypeString(plugin)} '{plugin.Name}'"))
        {
            plugins.TryAdd(plugin, plugin.GetType().Assembly);
            Debugger.Log($"{LOAD_SUCCESS} {GetSimpleTypeString(plugin)} '{plugin.Name}'");

            //  Load kernel modules from the plugin
            SwordfishEngine.Kernel.Load(plugin.GetType().Assembly);

            Debugger.TryInvoke(plugin.Initialize, $"{INITIALIZE_ERROR} {GetSimpleTypeString(plugin)} '{plugin.Name}'");
        }
    }

    public void LoadFrom(IPath path, SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        Debugger.TryInvoke(() => LoadFromInternal(path, searchOption), $"{LOADFROM_ERROR} '{path}'");
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
            if (!type.IsInterface && !type.IsAbstract && typeof(IPlugin).IsAssignableFrom(type))
            {
                if (IsLoaded(type))
                {
                    Debugger.Log($"{DUPLICATE_ERROR} {GetSimpleTypeString(type)} '{type}' at '{type.Assembly.Location}'", LogType.WARNING);
                    continue;
                }

                if (Activator.CreateInstance(type) is IPlugin plugin && Debugger.TryInvoke(plugin.Load, $"{LOAD_ERROR} {GetSimpleTypeString(type)} '{plugin.Name}'"))
                {
                    loadedPlugins.Add(plugin);
                    plugins.TryAdd(plugin, type.Assembly);

                    //  Load kernel modules from the plugin
                    SwordfishEngine.Kernel.Load(plugin.GetType().Assembly);

                    Debugger.Log($"{LOAD_SUCCESS} {GetSimpleTypeString(type)} '{plugin.Name}'");
                }
            }
        }

        foreach (IPlugin plugin in loadedPlugins)
            Debugger.TryInvoke(plugin.Initialize, $"{INITIALIZE_ERROR} {GetSimpleTypeString(plugin)} '{plugin.Name}'");
    }

    private void UnloadInternal(IPlugin plugin)
    {
        Debugger.TryInvoke(plugin.Unload, $"{UNLOAD_ERROR} {GetSimpleTypeString(plugin)} '{plugin.Name}'");
        plugins.TryRemove(plugin, out _);
    }

    private static string GetSimpleTypeString(IPlugin plugin) => GetSimpleTypeString(plugin.GetType());
    private static string GetSimpleTypeString(Type type)
    {
        return typeof(Mod).IsAssignableFrom(type) ? "mod" : "plugin";
    }
}

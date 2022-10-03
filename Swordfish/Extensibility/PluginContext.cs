using System.Collections.Concurrent;
using System.Reflection;
using Ninject;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.IO;

namespace Swordfish.Extensibility;

public class PluginContext : IPluginContext
{
    private const string DUPLICATE_ERROR = "Tried to load a duplicate";
    private const string LOADFROM_ERROR = "Failed to load extensions from";
    private const string INITIALIZE_ERROR = "Failed to initialize";
    private const string UNLOAD_ERROR = "Failed to unload";
    private const string LOAD_ERROR = "Failed to load";
    private const string LOAD_SUCCESS = "Loaded";
    private const string MISSING_DESCRIPTION = "Missing description!";

    private readonly ConcurrentDictionary<IPlugin, Assembly> LoadedPlugins = new();

    public void Initialize()
    {
        foreach (IPlugin plugin in LoadedPlugins.Keys)
            Debugger.TryInvoke(plugin.Initialize, $"{INITIALIZE_ERROR} {GetSimpleTypeString(plugin)} '{plugin.Name}'");
    }

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
        return LoadedPlugins.Keys.Any(p => p.GetType() == type);
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
            LoadedPlugins.TryAdd(plugin, plugin.GetType().Assembly);

            //  Load kernel modules from the plugin
            SwordfishEngine.Kernel.Load(plugin.GetType().Assembly);

            Debugger.Log($"{LOAD_SUCCESS} {GetSimpleTypeString(plugin)} '{plugin.Name}'");
            Debugger.Log(string.IsNullOrWhiteSpace(plugin.Description) ? MISSING_DESCRIPTION : plugin.Description, LogType.CONTINUED);
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
        foreach (IPlugin p in LoadedPlugins.Keys.Where(p => p.GetType() == type))
            UnloadInternal(p);
    }

    public void UnloadAll()
    {
        foreach (IPlugin p in LoadedPlugins.Keys)
            UnloadInternal(p);
    }

    private void LoadFromInternal(IPath path, SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        string[] files = Directory.GetFiles(path.OriginalString ?? string.Empty, "*.dll", searchOption);

        if (files.Length == 0)
            return;

        List<Assembly> loadedAssemblies = new();
        foreach (string file in files)
        {
            try
            {
                loadedAssemblies.Add(Assembly.LoadFrom(file));
            }
            catch
            {
                //  ignored
            }
        }

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
                    LoadedPlugins.TryAdd(plugin, type.Assembly);

                    //  Load kernel modules from the plugin
                    SwordfishEngine.Kernel.Load(plugin.GetType().Assembly);

                    Debugger.Log($"{LOAD_SUCCESS} {GetSimpleTypeString(type)} '{plugin.Name}'");
                    Debugger.Log(string.IsNullOrWhiteSpace(plugin.Description) ? MISSING_DESCRIPTION : plugin.Description, LogType.CONTINUED);
                }
            }
        }
    }

    private void UnloadInternal(IPlugin plugin)
    {
        Debugger.TryInvoke(plugin.Unload, $"{UNLOAD_ERROR} {GetSimpleTypeString(plugin)} '{plugin.Name}'");
        LoadedPlugins.TryRemove(plugin, out _);
    }

    private static string GetSimpleTypeString(IPlugin plugin) => GetSimpleTypeString(plugin.GetType());
    private static string GetSimpleTypeString(Type type)
    {
        return typeof(Mod).IsAssignableFrom(type) ? "mod" : "plugin";
    }
}

using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.IO;

namespace Swordfish.Extensibility;

public class PluginContext : IPluginContext
{
    private const string DUPLICATE_ERROR = "Tried to load a duplicate";
    private const string LOADFROM_ERROR = "Failed to load extensions from";
    private const string UNLOAD_ERROR = "Failed to unload";
    private const string LOAD_ERROR = "Failed to load";
    private const string LOAD_SUCCESS = "Loaded";
    private const string MISSING_DESCRIPTION = "Missing description!";

    private readonly ConcurrentDictionary<Type, Assembly> PluginTypes = new();
    private readonly ConcurrentDictionary<Type, IPlugin> ActivePlugins = new();

    public IEnumerable<Type> GetRegisteredTypes()
    {
        return PluginTypes.Keys;
    }

    public void InvokeStart(IEnumerable<IPlugin> plugins)
    {
        //  TODO plugins should be given their own threads
        //  TODO re-enable this once the GL renderer is made thread-safe
        // Parallel.ForEach(plugins, ForEachPlugin);
        // ThreadPool.QueueUserWorkItem(WorkCallback);
        // void WorkCallback(object? state) => Parallel.ForEach(plugins, ForEachPlugin);

        foreach (var plugin in plugins)
        {
            ForEachPlugin(plugin, null, 0);
        }

        void ForEachPlugin(IPlugin plugin, ParallelLoopState loopState, long index)
        {
            if (Debugger.TryInvoke(plugin.Start, $"{LOAD_ERROR} {GetSimpleTypeString(plugin)} '{plugin.Name}'"))
            {
                Debugger.Log($"{LOAD_SUCCESS} {GetSimpleTypeString(plugin)} '{plugin.Name}'");
                Debugger.Log(string.IsNullOrWhiteSpace(plugin.Description) ? MISSING_DESCRIPTION : plugin.Description, LogType.CONTINUED);
            }
        }
    }

    public void Register(params Assembly[] assemblies)
    {
        IEnumerable<Type> types = assemblies.SelectMany(assembly => assembly.GetTypes());

        foreach (Type type in types)
        {
            if (!type.IsInterface && !type.IsAbstract && typeof(IPlugin).IsAssignableFrom(type))
            {
                if (IsLoaded(type))
                {
                    Debugger.Log($"{DUPLICATE_ERROR} {GetSimpleTypeString(type)} '{type}' at '{type.Assembly.Location}'", LogType.WARNING);
                    continue;
                }

                PluginTypes.TryAdd(type, type.Assembly);
            }
        }
    }

    public void RegisterFrom(IPath path, SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        string[] files = Directory.GetFiles(path.OriginalString ?? string.Empty, "*.dll", searchOption);

        List<Assembly> loadedAssemblies = new();
        foreach (string file in files)
            Debugger.TryInvoke(() => loadedAssemblies.Add(Assembly.LoadFrom(file)));

        Register(loadedAssemblies.ToArray());
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
        return ActivePlugins.TryGetValue(type, out _);
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
        if (ActivePlugins.TryGetValue(type, out IPlugin? plugin))
            UnloadInternal(plugin);
    }

    public void UnloadAll()
    {
        foreach (IPlugin p in ActivePlugins.Values)
            UnloadInternal(p);
    }

    private void UnloadInternal(IPlugin plugin)
    {
        Debugger.TryInvoke(plugin.Unload, $"{UNLOAD_ERROR} {GetSimpleTypeString(plugin)} '{plugin.Name}'");
        PluginTypes.TryRemove(plugin.GetType(), out _);
    }

    private static string GetSimpleTypeString(IPlugin plugin) => GetSimpleTypeString(plugin.GetType());
    private static string GetSimpleTypeString(Type type)
    {
        return typeof(Mod).IsAssignableFrom(type) ? "mod" : "plugin";
    }
}

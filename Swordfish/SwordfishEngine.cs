using Ninject;
using Swordfish.Graphics;
using Swordfish.Library.IO;
using Swordfish.Extensibility;

namespace Swordfish;

public static class SwordfishEngine
{
    public readonly static IKernel Kernel;
    public readonly static IWindowContext MainWindow;

    private readonly static IPathService PathService;
    private readonly static IPluginContext PluginContext;

    static SwordfishEngine()
    {
        Kernel = new StandardKernel();
        Kernel.Load(typeof(SwordfishEngine).Assembly);

        MainWindow = Kernel.Get<IWindowContext>();
        PathService = Kernel.Get<IPathService>();
        PluginContext = Kernel.Get<IPluginContext>();
    }

    static void Main(string[] args)
    {
        MainWindow.Load += Start;
        MainWindow.Close += Stop;
        MainWindow.Initialize();
    }

    private static void Start()
    {
        PluginContext.LoadFrom(PathService.Root);
        PluginContext.LoadFrom(PathService.Plugins, SearchOption.AllDirectories);
        PluginContext.LoadFrom(PathService.Mods, SearchOption.AllDirectories);
    }

    private static void Stop()
    {
        PluginContext.UnloadAll();
    }
}

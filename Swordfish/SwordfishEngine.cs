using Ninject;
using Swordfish.Library.IO;
using Swordfish.Plugins;
using Swordfish.Rendering;

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
        Kernel.Load(AppDomain.CurrentDomain.GetAssemblies());

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
    }

    private static void Stop()
    {
        PluginContext.UnloadAll();
    }
}

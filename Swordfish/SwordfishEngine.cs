using System.Runtime.InteropServices;
using Ninject;
using Swordfish.ECS;
using Swordfish.Extensibility;
using Swordfish.Graphics;
using Swordfish.Library.IO;

namespace Swordfish;

public static class SwordfishEngine
{
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool AllocConsole();

    [DllImport("kernel32", SetLastError = true)]
    static extern bool AttachConsole(int dwProcessId);

    public readonly static Version? Version;
    public readonly static IKernel Kernel;
    public readonly static IWindowContext MainWindow;

    private readonly static IPathService PathService;
    private readonly static IPluginContext PluginContext;
    private readonly static IECSContext ECSContext;

    static SwordfishEngine()
    {
        Version = typeof(SwordfishEngine).Assembly.GetName().Version;
        Kernel = new StandardKernel();
        Kernel.Load(typeof(SwordfishEngine).Assembly);

        MainWindow = Kernel.Get<IWindowContext>();
        PathService = Kernel.Get<IPathService>();
        PluginContext = Kernel.Get<IPluginContext>();
        ECSContext = Kernel.Get<IECSContext>();
    }

    static void Main(string[] args)
    {
        if (args.Contains("-debug") && !AttachConsole(-1))
            AllocConsole();

        MainWindow.Load += Start;
        MainWindow.Close += Stop;
        MainWindow.Initialize();
    }

    private static void Start()
    {
        PluginContext.LoadFrom(PathService.Root);
        PluginContext.LoadFrom(PathService.Plugins, SearchOption.AllDirectories);
        PluginContext.LoadFrom(PathService.Mods, SearchOption.AllDirectories);

        ECSContext.Start();

        PluginContext.Initialize();
    }

    private static void Stop()
    {
        PluginContext.UnloadAll();
        ECSContext.Stop();
    }
}

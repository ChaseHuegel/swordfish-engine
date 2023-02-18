using System.Runtime.InteropServices;
using DryIoc;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Swordfish.ECS;
using Swordfish.Extensibility;
using Swordfish.Graphics;
using Swordfish.Input;
using Swordfish.Library.Collections;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.IO;
using Swordfish.UI;

namespace Swordfish;

public static class SwordfishEngine
{
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool AllocConsole();

    [DllImport("kernel32", SetLastError = true)]
    static extern bool AttachConsole(int dwProcessId);

    public static Version Version { get; private set; }

    public static Kernel Kernel { get; private set; }

    private static IWindow MainWindow;
    private static IPluginContext PluginContext;
    private static IPathService EnginePathService;
    private static IInputContext? InputContext;
    private static GL? GL;

    static SwordfishEngine()
    {
        Version = typeof(SwordfishEngine).Assembly.GetName().Version!;
        Kernel = new Kernel(null);

        var options = WindowOptions.Default;
        options.Size = new Vector2D<int>(800, 600);
        options.Title = "Swordfish";
        options.ShouldSwapAutomatically = true;

        MainWindow = Window.Create(options);
        MainWindow.Load += OnWindowLoaded;
        MainWindow.Closing += OnWindowClosing;
    }

    static void Main(string[] args)
    {
        if (args.Contains("-debug") && !AttachConsole(-1))
            AllocConsole();

        MainWindow.Run();
    }

    private static void OnWindowClosing()
    {
        Kernel.Get<IPluginContext>().UnloadAll();
        Kernel.Get<IECSContext>().Stop();
    }

    private static void OnWindowLoaded()
    {
        EnginePathService = new PathService();
        PluginContext = new PluginContext();
        InputContext = MainWindow.CreateInput();
        GL = MainWindow.CreateOpenGL();

        PluginContext.RegisterFrom(EnginePathService.Root);
        PluginContext.RegisterFrom(EnginePathService.Plugins, SearchOption.AllDirectories);
        PluginContext.RegisterFrom(EnginePathService.Mods, SearchOption.AllDirectories);

        var resolver = new Container();
        resolver.RegisterInstance(PluginContext);
        resolver.RegisterInstance(MainWindow);
        resolver.RegisterInstance(InputContext);
        resolver.RegisterInstance(GL);

        resolver.RegisterMany(PluginContext.GetRegisteredTypes(), Reuse.Singleton);

        resolver.Register<IWindowContext, SilkWindowContext>(Reuse.Singleton);
        resolver.Register<IRenderContext, RenderContext>(Reuse.Singleton);
        resolver.Register<IUIContext, ImGuiContext>(Reuse.Singleton);

        resolver.Register<IECSContext, ECSContext>(Reuse.Singleton);

        resolver.Register<IInputService, SilkInputService>(Reuse.Singleton);
        resolver.Register<IShortcutService, ShortcutService>(Reuse.Singleton);

        resolver.Register<IPathService, PathService>(Reuse.Singleton);
        resolver.Register<IFileService, FileService>(Reuse.Singleton);

        resolver.ValidateAndThrow();
        Kernel = new Kernel(resolver);

        Start();
    }

    private static void Start()
    {
        IEnumerable<IPlugin> plugins = Kernel.GetAll<IPlugin>();

        //  Touch each plugin to trigger the ctor
        foreach (IPlugin plugin in plugins)
            Debugger.Log($"Initialized plugin '{plugin.Name}'.");

        Kernel.Get<IECSContext>().Start();
        PluginContext.InvokeStart(plugins);
    }
}

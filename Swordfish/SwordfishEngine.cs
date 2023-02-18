using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using SimpleInjector;
using Swordfish.ECS;
using Swordfish.Extensibility;
using Swordfish.Graphics;
using Swordfish.Input;
using Swordfish.Library.Collections;
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
    }

    static void Main(string[] args)
    {
        if (args.Contains("-debug") && !AttachConsole(-1))
            AllocConsole();

        MainWindow.Load += OnWindowLoaded;
        MainWindow.Closing += OnWindowClosing;
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

        var engineResolver = new Container();
        engineResolver.RegisterInstance(MainWindow);
        engineResolver.RegisterInstance(InputContext);
        engineResolver.RegisterInstance(GL);

        engineResolver.Collection.Register<IPlugin>(PluginContext.GetRegisteredTypes(), Lifestyle.Singleton);

        engineResolver.Register<IWindowContext, SilkWindowContext>(Lifestyle.Singleton);
        engineResolver.Register<IRenderContext, RenderContext>(Lifestyle.Singleton);
        engineResolver.Register<IUIContext, ImGuiContext>(Lifestyle.Singleton);

        engineResolver.Register<IECSContext, ECSContext>(Lifestyle.Singleton);

        engineResolver.Register<IPluginContext, PluginContext>(Lifestyle.Singleton);

        engineResolver.Register<IInputService, SilkInputService>(Lifestyle.Singleton);
        engineResolver.Register<IShortcutService, ShortcutService>(Lifestyle.Singleton);

        engineResolver.Register<IPathService, PathService>(Lifestyle.Singleton);
        engineResolver.Register<IFileService, FileService>(Lifestyle.Singleton);

        engineResolver.Verify();
        Kernel = new Kernel(engineResolver);

        Start();
    }

    private static void Start()
    {
        Kernel.Get<IECSContext>().Start();
        PluginContext.InvokeStart(Kernel.GetAll<IPlugin>());
    }
}

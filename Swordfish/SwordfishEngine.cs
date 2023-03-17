using DryIoc;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Swordfish.ECS;
using Swordfish.Extensibility;
using Swordfish.Graphics;
using Swordfish.Graphics.SilkNET.OpenGL;
using Swordfish.Input;
using Swordfish.IO;
using Swordfish.Library.Collections;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.IO;
using Swordfish.UI;

namespace Swordfish;

public static class SwordfishEngine
{
    public static Version Version { get; private set; }

    private static Kernel? kernel;
    public static Kernel Kernel
    {
        get => kernel!;
        private set => kernel = value;
    }

    public static SynchronizationManager SyncManager;
    private static IWindow MainWindow;

    static SwordfishEngine()
    {
        Version = typeof(SwordfishEngine).Assembly.GetName().Version!;
        SyncManager = new SynchronizationManager();

        var options = WindowOptions.Default;
        options.Size = new Vector2D<int>(800, 600);
        options.Title = "Swordfish";
        options.ShouldSwapAutomatically = true;
        options.VSync = false;

        MainWindow = Window.Create(options);
        MainWindow.Load += OnWindowLoaded;
        MainWindow.Closing += OnWindowClosing;
        MainWindow.Update += OnWindowUpdate;
    }

    static void Main(string[] args)
    {
#if WINDOWS
        if (args.Contains("-debug") && !Kernel32.AttachConsole(-1))
            Kernel32.AllocConsole();
#endif

        MainWindow.Run();
    }

    private static void OnWindowLoaded()
    {
        var enginePathService = new PathService();
        var pluginContext = new PluginContext();
        var inputContext = MainWindow.CreateInput();
        var gl = MainWindow.CreateOpenGL();

        pluginContext.RegisterFrom(enginePathService.Root);
        pluginContext.RegisterFrom(enginePathService.Plugins, SearchOption.AllDirectories);
        pluginContext.RegisterFrom(enginePathService.Mods, SearchOption.AllDirectories);

        var resolver = new Container();
        resolver.RegisterInstance<IPluginContext>(pluginContext);
        resolver.RegisterMany(pluginContext.GetRegisteredTypes(), Reuse.Singleton);

        resolver.RegisterInstance<GL>(gl);
        resolver.RegisterInstance<IWindow>(MainWindow);
        resolver.RegisterInstance<SynchronizationContext>(SyncManager);
        resolver.Register<GLContext>(Reuse.Singleton);
        resolver.Register<IWindowContext, SilkWindowContext>(Reuse.Singleton);
        resolver.Register<IRenderContext, GLRenderContext>(Reuse.Singleton);
        resolver.Register<IUIContext, ImGuiContext>(Reuse.Singleton);

        resolver.Register<IECSContext, ECSContext>(Reuse.Singleton);

        resolver.RegisterInstance<IInputContext>(inputContext);
        resolver.Register<IInputService, SilkInputService>(Reuse.Singleton);
        resolver.Register<IShortcutService, ShortcutService>(Reuse.Singleton);

        resolver.RegisterInstance<IPathService>(enginePathService);
        resolver.Register<IFileService, FileService>(Reuse.Singleton);
        resolver.Register<IFileParser, GlslParser>(Reuse.Singleton);
        resolver.Register<IFileParser, TextureParser>(Reuse.Singleton);

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
        Kernel.Get<IPluginContext>().InvokeStart(plugins);
    }

    private static void OnWindowClosing()
    {
        Kernel.Get<IPluginContext>().UnloadAll();
        Kernel.Get<IECSContext>().Stop();
    }

    private static void OnWindowUpdate(double obj)
    {
        SyncManager.Process();
    }
}

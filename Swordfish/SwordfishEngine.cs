using DryIoc;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Swordfish.ECS;
using Swordfish.Entities;
using Swordfish.Extensibility;
using Swordfish.Graphics;
using Swordfish.Graphics.Jolt;
using Swordfish.Graphics.SilkNET.OpenGL;
using Swordfish.Graphics.SilkNET.OpenGL.Renderers;
using Swordfish.Input;
using Swordfish.IO;
using Swordfish.Library.Collections;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.IO;
using Swordfish.Library.Threading;
using Swordfish.Physics.Jolt;
using Swordfish.Settings;
using Swordfish.UI;

namespace Swordfish;

public class SwordfishEngine
{
    public enum EngineState
    {
        Stopped,
        Starting,
        Started,
        Initializing,
        Initialized,
        Loading,
        Loaded,
        Waking,
        Awake,
        Running,
        Closing,
        Closed,
        Stopping
    }

    //  ? Should there be an injected metadata service to contain additional details?
    public static Version Version => version ??= typeof(SwordfishEngine).Assembly.GetName().Version!;
    private static Version version;

    //  TODO making this non-static requires refactoring ECS to use DI
    private static Kernel? kernel;
    public static Kernel Kernel
    {
        get => kernel!;
        private set => kernel = value;
    }

    public ThreadContext MainThreadContext { get; set; }
    public EngineState State { get; private set; }
    private IWindow MainWindow { get; set; }
    private int ExitCode { get; set; }

    public void Stop(int exitCode = 0)
    {
        TransitionState(EngineState.Initialized, EngineState.Stopping);
        ExitCode = exitCode;
        MainWindow.Close();
    }

    public int Run(params string[] args)
    {
        TransitionState(EngineState.Stopped, EngineState.Starting);

#if WINDOWS
        if (args.Contains("-debug") && !Kernel32.AttachConsole(-1))
            Kernel32.AllocConsole();
#endif

        TransitionState(EngineState.Starting, EngineState.Started);
        Initialize();
        MainWindow.Run();
        TransitionState(EngineState.Closed, EngineState.Stopped);
        return ExitCode;
    }

    private void Initialize()
    {
        TransitionState(EngineState.Started, EngineState.Initializing);

        MainThreadContext = ThreadContext.FromCurrentThread();
        SynchronizationContext.SetSynchronizationContext(MainThreadContext);

        var options = WindowOptions.Default;
        options.Size = new Vector2D<int>(800, 600);
        options.Title = "Swordfish";
        options.ShouldSwapAutomatically = true;
        options.VSync = false;

        MainWindow = Window.Create(options);
        MainWindow.Load += OnWindowLoaded;
        MainWindow.Closing += OnWindowClosing;
        MainWindow.Update += OnWindowUpdate;

        TransitionState(EngineState.Initializing, EngineState.Initialized);
    }

    private void OnWindowLoaded()
    {
        TransitionState(EngineState.Initialized, EngineState.Loading);

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
        resolver.RegisterInstance<SynchronizationContext>(MainThreadContext);
        resolver.Register<GLContext>(Reuse.Singleton);
        resolver.Register<IWindowContext, SilkWindowContext>(Reuse.Singleton);
        resolver.RegisterMany<GLRenderContext>(Reuse.Singleton);
        resolver.Register<IRenderStage, GLInstancedRenderer>(ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation);
        resolver.Register<IUIContext, ImGuiContext>(Reuse.Singleton);
        resolver.RegisterMany<GLLineRenderer>(Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation);
        resolver.Register<IRenderStage, JoltDebugRenderer>(Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation);

        resolver.Register<IECSContext, ECSContext>(Reuse.Singleton);
        resolver.RegisterMany<JoltPhysicsSystem>(Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation, nonPublicServiceTypes: true);
        resolver.Register<IEntitySystem, MeshRendererSystem>();

        resolver.RegisterInstance<IInputContext>(inputContext);
        resolver.Register<IInputService, SilkInputService>(Reuse.Singleton);
        resolver.Register<IShortcutService, ShortcutService>(Reuse.Singleton);

        resolver.RegisterInstance<IPathService>(enginePathService);
        resolver.Register<IFileService, FileService>(Reuse.Singleton);
        resolver.Register<IFileParser, GlslParser>(Reuse.Singleton);
        resolver.Register<IFileParser, TextureParser>(Reuse.Singleton);
        resolver.Register<IFileParser, TextureArrayParser>(Reuse.Singleton);
        resolver.Register<IFileParser, OBJParser>(Reuse.Singleton);
        resolver.Register<IFileParser, LegacyVoxelObjectParser>(Reuse.Singleton);

        var renderSettings = new RenderSettings();
        var debugSettings = new DebugSettings();
        debugSettings.Stats.Set(true);

        resolver.RegisterInstance<RenderSettings>(renderSettings);
        resolver.RegisterInstance<DebugSettings>(debugSettings);

        resolver.ValidateAndThrow();
        Kernel = new Kernel(resolver);

        TransitionState(EngineState.Loading, EngineState.Loaded);
        Awake();
    }

    private void Awake()
    {
        TransitionState(EngineState.Loaded, EngineState.Waking);

        Kernel.Get<IRenderContext>();

        IEnumerable<IPlugin> plugins = Kernel.GetAll<IPlugin>();

        //  Touch each plugin to trigger the ctor
        foreach (IPlugin plugin in plugins)
            Debugger.Log($"Initialized plugin '{plugin.Name}'.");

        Kernel.Get<IECSContext>().Start();
        Kernel.Get<IPluginContext>().Activate(plugins);

        TransitionState(EngineState.Waking, EngineState.Awake);
    }

    private void OnWindowClosing()
    {
        TransitionState(EngineState.Initialized, EngineState.Closing);

        Kernel.Get<IPluginContext>().UnloadAll();
        Kernel.Get<IECSContext>().Stop();

        TransitionState(EngineState.Closing, EngineState.Closed);
    }

    private void OnWindowUpdate(double obj)
    {
        if (State != EngineState.Running)
            TransitionState(EngineState.Awake, EngineState.Running);

        MainThreadContext.ProcessMessageQueue();
    }

    private void TransitionState(EngineState expectedState, EngineState newState)
    {
        //  If we are trying to transition to incompatible states that is a fatal issue,
        //  something will certainly go horribly wrong if it hasn't already. Burn it down!
        if (State < expectedState)
            throw new FatalAlertException($"Unable to transition to state {newState}, current state is: {State} but expected {expectedState}.");

        Debugger.Log($"Engine state transitioning from {State} to {newState}.");
        State = newState;
    }
}

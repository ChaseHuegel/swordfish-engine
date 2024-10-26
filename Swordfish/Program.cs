using DryIoc;
using Microsoft.Extensions.Logging;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using Swordfish.AppEngine;
using Swordfish.ECS;
using Swordfish.Extensibility;
using Swordfish.Graphics;
using Swordfish.Library.Collections;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.Threading;

namespace Swordfish;

internal static class Program
{
    private enum EngineState
    {
        Stopped,
        Initializing,
        Initialized,
        Starting,
        Started,
        Loading,
        Loaded,
        Waking,
        Awake,
        Running,
        Closing,
        Closed,
        Stopping
    }
    
    public static readonly ThreadContext MainThreadContext;
    public static readonly IWindow MainWindow;

    private static readonly ILogger _logger = Engine.CreateLogger<Engine>();
    private static readonly Engine _engine = new();
    private static EngineState _state;
    private static int _exitCode;

    static Program()
    {
        TransitionState(EngineState.Stopped, EngineState.Initializing);
        
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

    private static int Main(string[] args)
    {
        TransitionState(EngineState.Initialized, EngineState.Starting);
#if WINDOWS
        if (args.Contains("-debug") && !Kernel32.AttachConsole(-1))
            Kernel32.AllocConsole();
#endif
        TransitionState(EngineState.Starting, EngineState.Started);
        
        MainWindow.Run();
        TransitionState(EngineState.Closed, EngineState.Stopped);
        return _exitCode;
    }
    
    internal static void Stop(int exitCode = 0)
    {
        TransitionState(EngineState.Started, EngineState.Stopping);
        _exitCode = exitCode;
        MainWindow.Close();
    }
    
    private static void OnWindowLoaded()
    {
        TransitionState(EngineState.Started, EngineState.Loading);
        _engine.Start([]);
        SwordfishEngine.Kernel = new Kernel(_engine.Container); //  TODO get rid of this
        TransitionState(EngineState.Loading, EngineState.Loaded);
        
        TransitionState(EngineState.Loaded, EngineState.Waking);
        _engine.Container.Resolve<IRenderContext>();

        //  Touch each plugin to trigger their ctor
        List<IPlugin> plugins = _engine.Container.ResolveMany<IPlugin>().ToList();
        foreach (IPlugin plugin in plugins)
        {
            _logger.LogInformation("Initialized plugin '{plugin}'.", plugin.Name);
        }

        _engine.Container.Resolve<IECSContext>().Start();
        _engine.Container.Resolve<IPluginContext>().Activate(plugins);
        TransitionState(EngineState.Waking, EngineState.Awake);
    }
    
    private static void OnWindowUpdate(double delta)
    {
        if (_state != EngineState.Running)
        {
            TransitionState(EngineState.Awake, EngineState.Running);
        }

        MainThreadContext.ProcessMessageQueue();
    }
    
    private static void OnWindowClosing()
    {
        TransitionState(EngineState.Started, EngineState.Closing);
        _engine.Container.Resolve<IPluginContext>().UnloadAll();
        _engine.Container.Resolve<IECSContext>().Stop();
        TransitionState(EngineState.Closing, EngineState.Closed);
    }
    
    private static void TransitionState(EngineState expectedState, EngineState newState)
    {
        //  If we are trying to transition to incompatible states that is a fatal issue,
        //  something will certainly go horribly wrong if it hasn't already. Burn it down!
        if (_state < expectedState)
            throw new FatalAlertException($"Unable to transition to state {newState}, current state is: {_state} but expected {expectedState}.");

        _logger.LogInformation("Engine state transitioning from {state} to {newState}.", _state, newState);
        _state = newState;
    }
}
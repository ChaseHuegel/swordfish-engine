using DryIoc;
using Microsoft.Extensions.Logging;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using Shoal;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.Collections;
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

    private static readonly ILogger _logger = AppEngine.CreateLogger(typeof(Program));
    private static AppEngine? _engine;
    private static EngineState _state;
    private static string[]? _args;

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
        MainWindow.Update += OnWindowUpdate;
        TransitionState(EngineState.Initializing, EngineState.Initialized);
    }

    private static int Main(string[] args)
    {
        TransitionState(EngineState.Initialized, EngineState.Starting);
        _args = args;
#if WINDOWS
        if (args.Contains("-debug") && !Kernel32.AttachConsole(-1))
            Kernel32.AllocConsole();
#endif
        TransitionState(EngineState.Starting, EngineState.Started);
        
        MainWindow.Run();

        TransitionState(EngineState.Started, EngineState.Closing);
        if (_engine == null)
        {
            _logger.LogCritical($"The {nameof(AppEngine)} was null after closing the window, this is most unfortunate.");
            return Crash();
        }
        
        _engine.Container.Resolve<IECSContext>().Stop();    //  TODO turn this into a disposable
        TransitionState(EngineState.Closing, EngineState.Closed);

        _engine.Dispose();
        TransitionState(EngineState.Closed, EngineState.Stopped);

        return Environment.ExitCode;
    }
    
    internal static void Stop(int exitCode = 0)
    {
        TransitionState(EngineState.Started, EngineState.Stopping);
        Environment.ExitCode = exitCode;
        MainWindow.Close();
    }

    internal static int Crash(int exitCode = (int)ExitCode.Crash)
    {
        //  TODO collect any useful information, such as active callstacks, and dump a crash report.
        Environment.Exit(exitCode);
        return exitCode;
    }
    
    private static void OnWindowLoaded()
    {
        TransitionState(EngineState.Started, EngineState.Loading);
        if (_args == null)
        {
            _logger.LogCritical("The window has loaded with null args, it must have bypassed the main entry point.");
            Crash();
            return;
        }
        
        _engine = AppEngine.Build(_args, Console.Out);
        SwordfishEngine.Kernel = new Kernel(_engine.Container); //  TODO get rid of this
        TransitionState(EngineState.Loading, EngineState.Loaded);

        TransitionState(EngineState.Loaded, EngineState.Waking);
        _engine.Container.Resolve<IRenderContext>();    //  TODO turn this into an entry point
        _engine.Container.Resolve<IECSContext>().Start();   //  TODO turn this into an entry point
        _engine.Start();
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
    
    private static void TransitionState(EngineState expectedState, EngineState newState)
    {
        //  If we are trying to transition to incompatible states that is a fatal issue,
        //  something will certainly go horribly wrong if it hasn't already. Burn it down!
        if (_state < expectedState)
        {
            _logger.LogCritical("Unable to transition to state {newState}, current state is: {currentState} but expected {expectedState}.", newState, _state, expectedState);
            Crash((int)ExitCode.BadState);
        }

        _logger.LogDebug("Engine state transitioning from {currentState} to {newState}.", _state, newState);
        _state = newState;
    }
}
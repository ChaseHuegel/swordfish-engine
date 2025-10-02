using System.Drawing;
using Microsoft.Extensions.Logging;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using Shoal;
using Silk.NET.Core;
using Swordfish.Library.Threading;
using Swordfish.Util;

// ReSharper disable UnusedMember.Global

namespace Swordfish;

public class SwordfishEngine
{
    private enum State
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
        Stopping,
    }
    
    public static Version Version => _version ??= typeof(SwordfishEngine).Assembly.GetName().Version!;
    private static Version? _version;
    
    private readonly string[] _args;
    private readonly EngineContainer _engineContainer;
    private readonly ThreadContext _mainThreadContext;
    private readonly IWindow _mainWindow;
    private readonly ILogger _logger = AppEngine.CreateLogger(typeof(SwordfishEngine));
    
    private AppEngine? _engine;
    private State _state;
    private int _exitCode;

    public SwordfishEngine(string[] args)
    {
        TransitionState(State.Stopped, State.Initializing);
        _args = args;

        _mainThreadContext = ThreadContext.FromCurrentThread();
        SynchronizationContext.SetSynchronizationContext(_mainThreadContext);

        var options = WindowOptions.Default;
        options.Size = new Vector2D<int>(800, 600);
        options.Title = "Swordfish";
        options.ShouldSwapAutomatically = true;
        options.VSync = false;

        _mainWindow = Window.Create(options);
        _mainWindow.Load += OnWindowLoaded;
        _mainWindow.Update += OnWindowUpdate;

        _engineContainer = new EngineContainer(_mainWindow, _mainThreadContext);
        TransitionState(State.Initializing, State.Initialized);
    }

    public int Run()
    {
        TransitionState(State.Initialized, State.Starting);
#if WINDOWS
        if ((_args.Contains("-debug") || _args.Contains("--debug")) && !Kernel32.AttachConsole(-1))
        {
            Kernel32.AllocConsole();
        }
#endif
        TransitionState(State.Starting, State.Started);
        
        _mainWindow.Run();

        TransitionState(State.Started, State.Closing);
        if (_engine == null)
        {
            _logger.LogCritical($"The {nameof(AppEngine)} was null after closing the window, this is most unfortunate.");
            return (int)ExitCode.BadState;
        }
        
        TransitionState(State.Closing, State.Closed);

        _engine.Dispose();
        TransitionState(State.Closed, State.Stopped);

        return _exitCode;
    }
    
    public void Stop(int exitCode = 0)
    {
        TransitionState(State.Started, State.Stopping);
        _exitCode = exitCode;
        _mainWindow.Close();
    }
    
    private void OnWindowLoaded()
    {
        //  Default to setting the window icon from the EXE if this is on windows
        if (OperatingSystem.IsWindows())
        {
            string? executablePath = Environment.ProcessPath;
            if (executablePath != null)
            {
                var icon = Icon.ExtractAssociatedIcon(executablePath);
                if (icon != null)
                {
                    using var stream = new MemoryStream();
                    icon.Save(stream);
                    RawImage rawIcon = Imaging.LoadAsPng(stream);
                    _mainWindow.SetWindowIcon(ref rawIcon);
                }
            }
        }
        
        TransitionState(State.Started, State.Loading);
        _engine = AppEngine.Build(_args, _engineContainer.Register);
        TransitionState(State.Loading, State.Loaded);

        TransitionState(State.Loaded, State.Waking);
        _engine.Start();
        TransitionState(State.Waking, State.Awake);
    }
    
    private void OnWindowUpdate(double delta)
    {
        if (_state != State.Running)
        {
            TransitionState(State.Awake, State.Running);
        }

        _mainThreadContext.ProcessMessageQueue();
    }
    
    private void TransitionState(State expectedState, State newState)
    {
        //  If we are trying to transition to incompatible states that is a fatal issue,
        //  something will certainly go horribly wrong if it hasn't already.
        if (_state < expectedState)
        {
            _logger.LogCritical("Unable to transition to state {newState}, current state is: {currentState} but expected {expectedState}.", newState, _state, expectedState);
            Stop((int)ExitCode.BadState);
        }

        _logger.LogDebug("Engine state transitioning from {currentState} to {newState}.", _state, newState);
        _state = newState;
    }
}
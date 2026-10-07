using System;
using System.Diagnostics;
using System.Threading;
// ReSharper disable UnusedMember.Global

namespace Swordfish.Library.Threading;

public class ThreadWorker
{
    private volatile bool _stop;
    private volatile bool _pause;
    private volatile bool _started;

    private readonly Thread _thread;
    private readonly Action _handleOnce;
    private readonly Action<float> _handle;

    private readonly Stopwatch _stopwatch = new();

    public int TargetTickRate = 64;

    public float DeltaTime { get; private set; }
    private double _lastTickTime;

    public static ThreadWorker Start(Action handler, string name = "")
    {
        return new ThreadWorker(handler, name);
    }

    public ThreadWorker(Action handler, string name = "")
    {
        _handleOnce = handler;
        _thread = new Thread(new ThreadStart(_handleOnce))
        {
            Name = name == "" ? _handle.Method.ToString() : name,
            IsBackground = true,
        };
    }

    public ThreadWorker(Action<float> handler, string name = "")
    {
        _handle = handler;
        _thread = new Thread(Tick)
        {
            Name = name == "" ? _handle.Method.ToString() : name,
            IsBackground = true,
        };
    }

    public void Start()
    {
        _stop = false;
        _pause = false;
        _started = true;
        _thread.Start();
    }

    public void Stop()
    {
        _stop = true;
    }

    /// <summary>Blocks until the worker thread has exited; pair with <see cref="Stop"/> before teardown.</summary>
    public void Join()
    {
        if (_started)
        {
            _thread.Join();
        }
    }

    public void Restart()
    {
        _stop = false;
        _pause = false;
        _thread.Start();
    }

    public void Pause()
    {
        _pause = true;
    }

    public void Unpause()
    {
        _pause = false;
    }

    public void TogglePause()
    {
        if (_pause)
        {
            Unpause();
        }
        else
        {
            Pause();
        }
    }

    private void Tick()
    {
        _stopwatch.Start();

        while (_stop == false)
        {
            while (_pause == false && _stop == false)
            {
                //	If handle is no longer valid, stop the thread
                if (_handle == null)
                {
                    Stop();
                }

                double tickStart = _stopwatch.Elapsed.TotalSeconds;
                DeltaTime = (float)(tickStart - _lastTickTime);
                _lastTickTime = tickStart;

                _handle(DeltaTime);

                //	Limit thread by target tick rate to save resources. Rate of 0 is unlimited.
                if (TargetTickRate > 0)
                {
                    double targetTickDelta = 1.0 / TargetTickRate;
                    double tickTime = _stopwatch.Elapsed.TotalSeconds - tickStart;
                    double remaining = targetTickDelta - tickTime;

                    if (remaining > 0)
                    {
                        Thread.Sleep((int)(remaining * 1000));
                    }
                }
            }

            Thread.Sleep(200);  //	Sleep when paused
            _lastTickTime = _stopwatch.Elapsed.TotalSeconds;
        }
        //	Stopped thread safely
    }

    public static ThreadWorker Create(string name, Action handler) => new(handler, name);
    public static ThreadWorker Create(string name, Action<float> handler) => new(handler, name);

    public static ThreadWorker Run(string name, Action handler)
    {
        ThreadWorker worker = Create(name, handler);
        worker.Start();
        return worker;
    }

    public static ThreadWorker Run(string name, Action<float> handler)
    {
        ThreadWorker worker = Create(name, handler);
        worker.Start();
        return worker;
    }
}
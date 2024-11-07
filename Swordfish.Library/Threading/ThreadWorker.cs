using System;
using System.Diagnostics;
using System.Threading;
// ReSharper disable UnusedMember.Global

namespace Swordfish.Library.Threading;

public class ThreadWorker
{
    private volatile bool _stop;
    private volatile bool _pause;

    private readonly Thread _thread;
    private readonly Action _handleOnce;
    private readonly Action<float> _handle;

    private readonly Stopwatch _stopwatch = new();

    public int TargetTickRate = 64;

    public float DeltaTime { get; private set; }
    private float _elapsedTime;

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
        _thread.Start();
    }

    public void Stop()
    {
        _stop = true;
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
        while (_stop == false)
        {
            while (_pause == false && _stop == false)
            {
                //	If handle is no longer valid, stop the thread
                if (_handle == null)
                {
                    Stop();
                }

                _stopwatch.Restart();
                _handle(DeltaTime);

                //	Limit thread by target tick rate to save resources. Rate of 0 is unlimited.
                if (TargetTickRate > 0)
                {
                    _elapsedTime += DeltaTime;

                    float targetTickDelta = 1f / TargetTickRate;

                    if (_elapsedTime < targetTickDelta)
                    {
                        Thread.Sleep((int)((targetTickDelta - _elapsedTime) * 1000));
                    }
                    else
                    {
                        _elapsedTime = 0f;
                    }
                }

                DeltaTime = (float)_stopwatch.ElapsedTicks / Stopwatch.Frequency;
            }

            Thread.Sleep(200);  //	Sleep when paused
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
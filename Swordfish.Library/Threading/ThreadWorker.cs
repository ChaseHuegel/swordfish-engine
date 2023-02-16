using System;
using System.Diagnostics;
using System.Threading;

using Debugger = Swordfish.Library.Diagnostics.Debugger;

namespace Swordfish.Library.Threading
{
    public class ThreadWorker
    {
        private volatile bool stop = false;
        private volatile bool pause = false;

        private Thread thread = null;
        private Action handleOnce;
        private Action<float> handle;

        private Stopwatch stopwatch = new Stopwatch();

        public int TargetTickRate = 64;

        public float DeltaTime { get; private set; }
        private float elapsedTime;

        public ThreadWorker(Action handler, string name = "")
        {
            handleOnce = handler;
            thread = new Thread(new ThreadStart(handleOnce))
            {
                Name = name == "" ? this.handle.Method.ToString() : name
            };
        }

        public ThreadWorker(Action<float> handler, string name = "")
        {
            handle = handler;
            thread = new Thread(Tick)
            {
                Name = name == "" ? this.handle.Method.ToString() : name
            };
        }

        public void Start()
        {
            stop = false;
            pause = false;
            thread.Start();

            Debugger.Log($"Started thread '{thread.Name}'", "Threading");
        }

        public void Stop()
        {
            stop = true;
            Debugger.Log($"Stopped thread '{thread.Name}'", "Threading");
        }

        public void Restart()
        {
            stop = false;
            pause = false;
            thread.Start();

            Debugger.Log($"Restarted thread '{thread.Name}'", "Threading");
        }

        public void Pause()
        {
            pause = true;

            Debugger.Log($"Paused thread '{thread.Name}'", "Threading");
        }

        public void Unpause()
        {
            pause = false;

            Debugger.Log($"Resumed thread '{thread.Name}'", "Threading");
        }

        public void TogglePause()
        {
            if (pause)
                Unpause();
            else
                Pause();
        }

        private void Handle()
        {
            handleOnce();
            Stop();
        }

        private void Tick()
        {
            while (stop == false)
            {
                while (pause == false && stop == false)
                {
                    //	If handle is no longer valid, stop the thread
                    if (handle == null) Stop();

                    stopwatch.Restart();
                    handle(DeltaTime);

                    //	Limit thread by target tick rate to save resources. Rate of 0 is unlimited.
                    if (TargetTickRate > 0)
                    {
                        elapsedTime += DeltaTime;

                        float targetTickDelta = 1f / TargetTickRate;

                        if (elapsedTime < targetTickDelta)
                            Thread.Sleep((int)((targetTickDelta - elapsedTime) * 1000));
                        else
                            elapsedTime = 0f;
                    }

                    DeltaTime = (float)stopwatch.ElapsedTicks / Stopwatch.Frequency;
                }

                Thread.Sleep(200);  //	Sleep when paused
            }

            Debugger.Log($"Closed thread '{thread.Name}'", "Threading");
            //	Stopped thread safely
        }

        public static ThreadWorker Create(string name, Action handler) => new ThreadWorker(handler, name);
        public static ThreadWorker Create(string name, Action<float> handler) => new ThreadWorker(handler, name);

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
}
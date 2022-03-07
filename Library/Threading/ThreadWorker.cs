using System;
using System.Diagnostics;
using System.Threading;

using Debug = Swordfish.Library.Diagnostics.Debug;

namespace Swordfish.Library.Threading
{
	public class ThreadWorker
	{
		private volatile bool stop = false;
		private volatile bool pause = false;

		private Thread thread = null;
		private Action<float> handle;

		private Stopwatch stopwatch = new Stopwatch();

		public int TargetTickRate = 64;

        public float DeltaTime { get; private set; }
        private float elapsedTime;

        public ThreadWorker(Action<float> handle, bool runOnce = false, string name = "")
		{
			this.handle = handle;

			if (runOnce)
				this.thread = new Thread(Handle);
			else
				this.thread = new Thread(Tick);

            this.thread.Name = name == "" ? this.handle.Method.ToString() : name;
        }

		public void Start()
		{
			stop = false;
			pause = false;
			thread.Start();

			Debug.Log($"Started thread '{thread.Name}'", "Threading");
		}

		public void Stop()
		{
			stop = true;
			Debug.Log($"Stopped thread '{thread.Name}'", "Threading");
		}

		public void Restart()
		{
			stop = false;
			pause = false;
			thread.Start();

			Debug.Log($"Restarted thread '{thread.Name}'", "Threading");
		}

		public void Pause()
		{
			pause = true;

			Debug.Log($"Paused thread '{thread.Name}'", "Threading");
		}

		public void Unpause()
		{
			pause = false;

			Debug.Log($"Resumed thread '{thread.Name}'", "Threading");
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
			handle(1f);
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
                            Thread.Sleep((int) ((targetTickDelta - elapsedTime) * 1000));
						else
							elapsedTime = 0f;
                    }

					DeltaTime = (float)stopwatch.ElapsedTicks / Stopwatch.Frequency;
                }

				Thread.Sleep(200);	//	Sleep when paused
			}

            Debug.Log($"Closed thread '{thread.Name}'", "Threading");
            //	Stopped thread safely
        }
	}
}
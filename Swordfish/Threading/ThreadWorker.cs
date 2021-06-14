using System;
using System.Diagnostics;
using System.Threading;

namespace Swordfish.Threading
{
	public class ThreadWorker
	{
		private volatile bool stop = false;
		private volatile bool pause = false;

		private Thread thread = null;
		private Action handle;

		private Stopwatch stopwatch = new Stopwatch();
        public float DeltaTime { get; private set; }

        public ThreadWorker(Action handle, bool runOnce = false, string name = "")
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
		}

		public void Stop()
		{
			stop = true;
		}

		public void Restart()
		{
			stop = false;
			pause = false;
			thread.Abort();
			thread.Start();
		}

		public void Pause()
		{
			pause = true;
		}

		public void Unpause()
		{
			pause = false;
		}

		public void TogglePause()
		{
			pause = !pause;
		}

		public void Kill()
		{
            thread.Abort();
			stop = true;
			Debug.Log($"Killed thread '{thread.Name}'", "Threading");
		}

		private void Handle()
		{
			handle();
			Kill();
		}

		private void Tick()
		{
			Debug.Log($"Started thread '{thread.Name}'", "Threading");

			while (stop == false)
			{
				while (pause == false && stop == false)
				{
					//	If handle is no longer valid, kill the thread
					if (handle == null) Kill();

                    stopwatch.Restart();
                    handle();

                    DeltaTime = (float)stopwatch.ElapsedTicks / Stopwatch.Frequency;
                }

				Thread.Sleep(200);	//	Sleep when paused
			}

            Debug.Log($"Stopped thread '{thread.Name}'", "Threading");
            //	Stopped thread safely
        }
	}
}
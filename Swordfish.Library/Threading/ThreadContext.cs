using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Swordfish.Library.Threading
{
    public class ThreadContext : SynchronizationContext
    {
        private struct WorkItem
        {
            internal SendOrPostCallback Callback;
            internal object State;

            public WorkItem(SendOrPostCallback callback, object state)
            {
                Callback = callback;
                State = state;
            }
        }

        private struct BlockingWorkItem
        {
            internal SendOrPostCallback Callback;
            internal object State;
            internal EventWaitHandle Handle;

            public BlockingWorkItem(SendOrPostCallback callback, object state, EventWaitHandle handle)
            {
                Callback = callback;
                State = state;
                Handle = handle;
            }
        }

        private int ThreadId;
        private readonly ConcurrentQueue<WorkItem> WorkQueue = new ConcurrentQueue<WorkItem>();
        private readonly ConcurrentQueue<BlockingWorkItem> SignaledWorkQueue = new ConcurrentQueue<BlockingWorkItem>();

        private ThreadContext(int threadID)
        {
            ThreadId = threadID;
        }

        public void SwitchToCurrentThread()
        {
            ThreadId = Environment.CurrentManagedThreadId;
        }

        public static ThreadContext FromCurrentThread()
        {
            return new ThreadContext(Environment.CurrentManagedThreadId);
        }

        public override void Post(SendOrPostCallback callback, object state)
        {
            WorkQueue.Enqueue(new WorkItem(callback, state));
        }

        public override void Send(SendOrPostCallback callback, object state)
        {
            if (ThreadId == Environment.CurrentManagedThreadId)
            {
                //  Invoke immediately if we're already on the correct thread.
                callback?.Invoke(state);
                return;
            }

            AutoResetEvent handle = new AutoResetEvent(false);
            SignaledWorkQueue.Enqueue(new BlockingWorkItem(callback, state, handle));
            handle.WaitOne();
        }

        public void ProcessMessageQueue()
        {
            if (ThreadId != Environment.CurrentManagedThreadId)
                throw new InvalidOperationException($"A {typeof(ThreadContext)} must be processed on the thread that created it.");

            while (WorkQueue.TryDequeue(out WorkItem workItem))
            {
                workItem.Callback?.Invoke(workItem.State);
            }

            while (SignaledWorkQueue.TryDequeue(out BlockingWorkItem workItem))
            {
                workItem.Callback?.Invoke(workItem.State);
                workItem.Handle.Set();
            }
        }
    }
}
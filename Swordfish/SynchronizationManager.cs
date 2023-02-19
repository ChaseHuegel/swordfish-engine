using System.Collections.Concurrent;

namespace Swordfish;

public class SynchronizationManager : SynchronizationContext
{
    private struct WorkItem
    {
        internal SendOrPostCallback Callback;
        internal object? State;

        public WorkItem(SendOrPostCallback callback, object? state)
        {
            Callback = callback;
            State = state;
        }
    }

    private struct BlockingWorkItem
    {
        internal SendOrPostCallback Callback;
        internal object? State;
        internal EventWaitHandle Handle;

        public BlockingWorkItem(SendOrPostCallback callback, object? state, EventWaitHandle handle)
        {
            Callback = callback;
            State = state;
            Handle = handle;
        }
    }

    private readonly int ThreadId;
    private readonly ConcurrentQueue<WorkItem> WorkQueue = new();
    private readonly ConcurrentQueue<BlockingWorkItem> SignaledWorkQueue = new();

    public SynchronizationManager()
    {
        ThreadId = Environment.CurrentManagedThreadId;
    }

    public override void Post(SendOrPostCallback d, object? state)
    {
        if (ThreadId == Environment.CurrentManagedThreadId)
        {
            d?.Invoke(state);
            return;
        }

        WorkQueue.Enqueue(new WorkItem(d, state));
    }

    public override void Send(SendOrPostCallback d, object? state)
    {
        if (ThreadId == Environment.CurrentManagedThreadId)
        {
            d?.Invoke(state);
            return;
        }

        AutoResetEvent handle = new(false);
        SignaledWorkQueue.Enqueue(new BlockingWorkItem(d, state, handle));
        handle.WaitOne();
    }

    public void Process()
    {
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
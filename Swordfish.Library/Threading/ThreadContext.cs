#nullable enable
using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Swordfish.Library.Threading;

public class ThreadContext : SynchronizationContext
{
    private struct WorkItem(in SendOrPostCallback? callback, in object? state)
    {
        internal readonly SendOrPostCallback? Callback = callback;
        internal readonly object? State = state;
    }

    private struct BlockingWorkItem(in SendOrPostCallback? callback, in object? state, in EventWaitHandle handle)
    {
        internal readonly SendOrPostCallback? Callback = callback;
        internal readonly object? State = state;
        internal readonly EventWaitHandle Handle = handle;
    }

    private int _threadId;
    private readonly ConcurrentQueue<WorkItem> _workQueue = new();
    private readonly ConcurrentQueue<BlockingWorkItem> _signaledWorkQueue = new();

    private ThreadContext(int threadID)
    {
        _threadId = threadID;
    }

    public void SwitchToCurrentThread()
    {
        _threadId = Environment.CurrentManagedThreadId;
    }

    public static ThreadContext FromCurrentThread()
    {
        return new ThreadContext(Environment.CurrentManagedThreadId);
    }

    public override void Post(SendOrPostCallback? callback, object? state)
    {
        _workQueue.Enqueue(new WorkItem(callback, state));
    }

    public override void Send(SendOrPostCallback? callback, object? state)
    {
        if (_threadId == Environment.CurrentManagedThreadId)
        {
            //  Invoke immediately if we're already on the correct thread.
            callback?.Invoke(state);
            return;
        }

        var handle = new AutoResetEvent(false);
        _signaledWorkQueue.Enqueue(new BlockingWorkItem(callback, state, handle));
        handle.WaitOne();
    }

    public void ProcessMessageQueue()
    {
        if (_threadId != Environment.CurrentManagedThreadId)
        {
            throw new InvalidOperationException($"A {typeof(ThreadContext)} must be processed on the thread that created it.");
        }

        while (_workQueue.TryDequeue(out WorkItem workItem))
        {
            workItem.Callback?.Invoke(workItem.State);
        }

        while (_signaledWorkQueue.TryDequeue(out BlockingWorkItem workItem))
        {
            workItem.Callback?.Invoke(workItem.State);
            workItem.Handle.Set();
        }
    }
}
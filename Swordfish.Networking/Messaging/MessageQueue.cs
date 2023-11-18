
using System.Collections.Concurrent;
using Swordfish.Library.Threading;

namespace Swordfish.Networking.Messaging;

public class MessageQueue<TMessage> : IDisposable
{
    private volatile bool _disposed;

    private readonly ThreadWorker _threadWorker;
    private readonly AutoResetEvent _messageSignal = new(false);
    private readonly ConcurrentQueue<TMessage> _messages = new();

    public event EventHandler<TMessage>? NewMessage;

    public MessageQueue()
    {
        _threadWorker = new ThreadWorker(ProcessQueue, nameof(MessageQueue<TMessage>));
    }

    public void Start()
    {
        _threadWorker.Start();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _threadWorker.Stop();
        _messages.Clear();
        _messageSignal.Set();
    }

    public void Post(TMessage message)
    {
        _messages.Enqueue(message);
        _messageSignal.Set();
    }

    private void SafeInvokeReceived(TMessage message)
    {
        try
        {
            NewMessage?.Invoke(this, message);
        }
        catch
        {
            //  Swallow exceptions thrown by listeners.
        }
    }

    private void ProcessQueue()
    {
        do
        {
            while (!_disposed && _messages.TryDequeue(out TMessage message))
                SafeInvokeReceived(message);
        } while (!_disposed && _messageSignal.WaitOne());
    }
}
using System.Threading.Tasks;
using WaywardBeyond.Shared.Networking.Events;

namespace WaywardBeyond.Shared.Networking.Messaging;

public class PacketAwaiter<T>
{
    private readonly IMessageConsumer<T> _consumer;
    private readonly TaskCompletionSource<T> _tcs = new();

    public PacketAwaiter(IMessageConsumer<T> consumer)
    {
        _consumer = consumer;
        _consumer.NewMessage += OnNewMessage;
    }

    public Task<T> WaitAsync()
    {
        return _tcs.Task;
    }

    private void OnNewMessage(object? sender, MessageEventArgs<T> e)
    {
        _consumer.NewMessage -= OnNewMessage;
        _tcs.TrySetResult(e.Message);
    }
}

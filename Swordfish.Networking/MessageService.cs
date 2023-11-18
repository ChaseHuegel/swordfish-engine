namespace Swordfish.Networking;

public class MessageService<TMessage, TDestination> : IDisposable where TMessage : new()
{
    private readonly IReceiver<ArraySegment<byte>> _receiver;
    private readonly IWriter<ArraySegment<byte>, TDestination> _writer;
    private readonly ISerializer<TMessage> _serializer;
    private readonly IFilter<TMessage> _filter;

    public event EventHandler<TMessage>? Received;

    public MessageService(IReceiver<ArraySegment<byte>> receiver, IWriter<ArraySegment<byte>, TDestination> writer, ISerializer<TMessage> serializer, IFilter<TMessage> filter)
    {
        _receiver = receiver;
        _writer = writer;
        _serializer = serializer;
        _filter = filter;

        _receiver.Received += OnDataRead;
    }

    public void Start()
    {
        _receiver.BeginListening();
    }

    public void Dispose()
    {
        _receiver.Received -= OnDataRead;
        _receiver.Dispose();
        _writer.Dispose();
    }

    public void Send(TMessage message, TDestination destination)
    {
        ArraySegment<byte> data = _serializer.Serialize(message);
        _writer.Send(data, destination);
    }

    public Task SendAsync(TMessage message, TDestination destination)
    {
        ArraySegment<byte> data = _serializer.Serialize(message);
        return _writer.SendAsync(data, destination);
    }

    private void OnDataRead(object sender, ArraySegment<byte> data)
    {
        TMessage message = _serializer.Deserialize<TMessage>(data);

        if (_filter.Check(message))
            SafeInvokeReceived(message);
    }

    private void SafeInvokeReceived(TMessage message)
    {
        try
        {
            Received?.Invoke(this, message);
        }
        catch
        {
            //  Swallow exceptions thrown by listeners.
        }
    }
}
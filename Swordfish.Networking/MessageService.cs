using System;
namespace Swordfish.Networking;

public class MessageService<TMessage, TDestination> : MessageService<TMessage, TMessage, TDestination> where TMessage : new()
{
    public MessageService(IReceiver<ArraySegment<byte>> receiver, IWriter<ArraySegment<byte>, TDestination> writer, ITypeSerializer<TMessage, TMessage> serializer, IFilter<TMessage> filter) : base(receiver, writer, serializer, filter)
    {
    }
}

public class MessageService<TMessageIn, TMessageOut, TDestination> : IDisposable where TMessageIn : new()
{
    private readonly IReceiver<ArraySegment<byte>> _receiver;
    private readonly IWriter<ArraySegment<byte>, TDestination> _writer;
    private readonly ITypeSerializer<TMessageOut, TMessageIn> _serializer;
    private readonly IFilter<TMessageIn> _filter;

    public event EventHandler<TMessageIn>? Received;

    public MessageService(IReceiver<ArraySegment<byte>> receiver, IWriter<ArraySegment<byte>, TDestination> writer, ITypeSerializer<TMessageOut, TMessageIn> serializer, IFilter<TMessageIn> filter)
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

    public void Send(TMessageOut message, TDestination destination)
    {
        ArraySegment<byte> data = _serializer.Serialize(message);
        _writer.Send(data, destination);
    }

    public Task SendAsync(TMessageOut message, TDestination destination)
    {
        ArraySegment<byte> data = _serializer.Serialize(message);
        return _writer.SendAsync(data, destination);
    }

    public void Send(ArraySegment<byte> data, TDestination destination)
    {
        _writer.Send(data, destination);
    }

    public Task SendAsync(ArraySegment<byte> data, TDestination destination)
    {
        return _writer.SendAsync(data, destination);
    }

    private void OnDataRead(object sender, ArraySegment<byte> data)
    {
        TMessageIn message = _serializer.Deserialize(data);

        if (_filter.Check(message))
            SafeInvokeReceived(message);
    }

    private void SafeInvokeReceived(TMessageIn message)
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
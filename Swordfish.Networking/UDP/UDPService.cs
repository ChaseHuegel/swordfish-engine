
using Needlefish;

namespace Swordfish.Networking.UDP;

public class UDPService<TMessage> : IDataService<TMessage> where TMessage : IDataBody, new()
{
    private readonly IDataService<DataEventArgs, TMessage> _dataService;

    private readonly ISerializer<TMessage> _serializer;

    private readonly IMessageProcessor<TMessage> _messageProcessor;

    public event EventHandler<TMessage>? Received;

    public UDPService(
        IDataService<DataEventArgs, TMessage> dataService,
        ISerializer<TMessage> serializer,
        IMessageProcessor<TMessage> messageProcessor
    )
    {
        _serializer = serializer;
        _dataService = dataService;
        _messageProcessor = messageProcessor;

        _dataService.Received += OnDataReceived;
        _messageProcessor.Received += OnMessageReceived;
    }

    public void Send(ArraySegment<byte> buffer, TMessage message)
    {
        _dataService.Send(buffer, message);
    }

    public Task SendAsync(ArraySegment<byte> buffer, TMessage message)
    {
        return _dataService.SendAsync(buffer, message);
    }

    public void Start()
    {
        _dataService.Start();
    }

    public void Dispose()
    {
        _dataService.Dispose();
    }

    private void SafeInvokeReceived(TMessage message)
    {
        try {
            Received?.Invoke(this, message);
        } catch {
            //  Swallow exceptions thrown by listeners.
        }
    }

    private void OnDataReceived(object sender, DataEventArgs e)
    {
        TMessage message = _serializer.Deserialize<TMessage>(e.Data);
        _messageProcessor.Post(message);
    }

    private void OnMessageReceived(object sender, TMessage message)
    {
        SafeInvokeReceived(message);
    }
}

namespace Swordfish.Networking;

public interface IMessageProcessor<TMessage>
{
    event EventHandler<TMessage>? Received;

    void Post(TMessage message);
}
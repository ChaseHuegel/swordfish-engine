namespace Swordfish.Networking;

public interface IReceiver<TMessage> : IDisposable
{
    event EventHandler<TMessage>? Received;

    void BeginListening();
}

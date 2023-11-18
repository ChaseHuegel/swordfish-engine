namespace Swordfish.Networking;

public interface IWriter<TMessage, TDestination> : IDisposable where TMessage : new()
{
    void Send(TMessage message, TDestination destination);

    Task SendAsync(TMessage message, TDestination destination);
}
namespace WaywardBeyond.Client.Networking;

/// <summary>The client's view of the active session. Reports the restored loopback after a remote drop.</summary>
internal sealed class GameClient(ClientRpcHost host)
{
    internal ClientRpcHost Host { get; } = host;

    public bool IsConnected => Host.IsConnected;

    public bool IsLocal => Host.IsLocal;
}

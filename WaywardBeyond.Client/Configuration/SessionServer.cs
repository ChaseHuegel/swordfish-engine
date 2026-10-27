namespace WaywardBeyond.Client.Configuration;

/// <summary>The server a session ran on.</summary>
public sealed record SessionServer
{
    /// <summary>Whether the session ran on the local host or a remote server.</summary>
    public ServerMode Mode { get; set; } = ServerMode.Local;

    /// <summary>The remote host, or empty for a local session.</summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>The remote port, or zero for a local session.</summary>
    public int Port { get; set; }
}

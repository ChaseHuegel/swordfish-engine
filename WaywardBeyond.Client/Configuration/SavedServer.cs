namespace WaywardBeyond.Client.Configuration;

/// <summary>A single saved server entry on the multiplayer page.</summary>
public sealed class SavedServer
{
    /// <summary>The display name shown in the favorites list.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The host to connect to.</summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>The port to connect to.</summary>
    public int Port { get; set; }
}

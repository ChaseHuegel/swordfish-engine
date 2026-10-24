using System;
using System.Collections.Generic;
using Swordfish.Library.Configuration;
using Swordfish.Library.Types;

namespace WaywardBeyond.Client.Configuration;

/// <summary>
/// Client-local state and preferences that are not real configuration: how the last session was joined
/// and the player's saved remote/LAN servers. Persisted to <c>profile.toml</c> alongside the other
/// configs, and the intended home for future client-local state (last character, etc.).
/// </summary>
public sealed class ProfileSettings : Config<ProfileSettings>
{
    /// <summary>Maximum saved-server entries; the list cannot grow unbounded in the config file.</summary>
    public const int MaxSavedServers = 32;

    /// <summary>How the last gameplay session was joined; Continue branches on this.</summary>
    public DataBinding<Configuration.LastServerMode> LastServerMode { get; private set; } = new(Configuration.LastServerMode.Local);

    /// <summary>The stable local user id. It is generated on first use and sent as the permission claim.</summary>
    public DataBinding<string> UserId { get; private set; } = new(string.Empty);

    /// <summary>The player's saved servers (connect targets), capped at 32 entries, deduped by host:port.</summary>
    public DataBinding<List<SavedServer>> SavedServers { get; private set; } = new([]);
}

/// <summary>A single saved server entry on the multiplayer page.</summary>
public sealed class SavedServer
{
    public string Name { get; set; } = string.Empty;

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; }
}
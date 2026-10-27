using System.Collections.Generic;
using Swordfish.Library.Configuration;
using Swordfish.Library.Types;
using WaywardBeyond.Data;

namespace WaywardBeyond.Client.Configuration;

/// <summary>
/// Client-local state and preferences that are not real configuration: the last played session, how
/// it was joined, and the player's saved remote servers. Persisted to <c>profile.toml</c>.
/// </summary>
public sealed class ProfileSettings : Config<ProfileSettings>
{
    /// <summary>Maximum saved-server entries; the list cannot grow unbounded in the config file.</summary>
    public const int MAX_SAVED_SERVERS = 32;

    /// <summary>How the last played session was joined; Continue branches on this.</summary>
    public DataBinding<LastServerMode> LastServerMode { get; private set; } = new(Configuration.LastServerMode.Local);

    /// <summary>The remote host used by the last played session.</summary>
    public DataBinding<string> LastServerHost { get; private set; } = new(string.Empty);

    /// <summary>The remote port used by the last played session.</summary>
    public DataBinding<int> LastServerPort { get; private set; } = new(0);

    /// <summary>The level GUID of the last played save.</summary>
    public DataBinding<string> LastSaveGuid { get; private set; } = new(string.Empty);

    /// <summary>The display name of the last played save, used by the Continue card before the listing loads.</summary>
    public DataBinding<string> LastSaveName { get; private set; } = new(string.Empty);

    /// <summary>The seed of the last played save, used by the Continue card before the listing loads.</summary>
    public DataBinding<int> LastSaveSeed { get; private set; } = new();

    /// <summary>The game mode of the last played save, used by the Continue card before the listing loads.</summary>
    public DataBinding<GameMode> LastSaveGameMode { get; private set; } = new(GameMode.Creative);

    /// <summary>The last played stamp of the last played save, used by the Continue card before the listing loads.</summary>
    public DataBinding<long> LastSaveLastPlayedMs { get; private set; } = new();

    /// <summary>The accumulated playtime of the last played save, used by the Continue card before the listing loads.</summary>
    public DataBinding<long> LastSaveAgeMs { get; private set; } = new();

    /// <summary>The UUID of the last played character.</summary>
    public DataBinding<string> LastCharacterUuid { get; private set; } = new(string.Empty);

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
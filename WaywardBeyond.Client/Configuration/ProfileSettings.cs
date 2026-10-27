using System.Collections.Generic;
using Swordfish.Library.Configuration;
using Swordfish.Library.Types;

namespace WaywardBeyond.Client.Configuration;

/// <summary>
/// Client-local state and preferences that are not real configuration: the last played session, and
/// the player's saved remote servers. Persisted to <c>profile.toml</c>.
/// </summary>
public sealed class ProfileSettings : Config<ProfileSettings>
{
    /// <summary>Maximum saved-server entries; the list cannot grow unbounded in the config file.</summary>
    public const int MAX_SAVED_SERVERS = 32;

    /// <summary>The last played session, which Continue resumes.</summary>
    public LastSession LastSession { get; private set; } = new();

    /// <summary>The stable local user id. It is generated on first use and sent as the permission claim.</summary>
    public DataBinding<string> UserId { get; private set; } = new(string.Empty);

    /// <summary>The player's saved servers (connect targets), capped at 32 entries, deduped by host:port.</summary>
    public DataBinding<List<SavedServer>> SavedServers { get; private set; } = new([]);
}

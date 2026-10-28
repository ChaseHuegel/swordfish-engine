using Tomlet.Attributes;
using WaywardBeyond.Data;

namespace WaywardBeyond.Client.Configuration;

/// <summary>The save a session used, as the Continue card shows it before the listing loads.</summary>
public sealed record SessionSave
{
    /// <summary>The level GUID, or empty when no save was recorded.</summary>
    public string Guid { get; set; } = string.Empty;

    /// <summary>The save's display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The level seed.</summary>
    public int Seed { get; set; }

    /// <summary>The level's default game mode.</summary>
    public GameMode GameMode { get; set; } = GameMode.Creative;

    /// <summary>The last played stamp, in Unix milliseconds.</summary>
    public long LastPlayedMs { get; set; }

    /// <summary>The accumulated playtime, in milliseconds.</summary>
    public long AgeMs { get; set; }

    /// <summary>Gets whether a save was recorded.</summary>
    [TomlNonSerialized]
    public bool IsRecorded => !string.IsNullOrWhiteSpace(Guid);
}

using System;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data.Components;

namespace WaywardBeyond.Gameplay.Stations;

/// <summary>Canonical station tag constants and client UI kind resolution.</summary>
public static class StationTags
{
    /// <summary>Station tag for power core generators.</summary>
    public const string CORE = "station.core";

    /// <summary>Station tag for general cargo storage containers.</summary>
    public const string STORAGE = "station.storage";

    /// <summary>Station tag for display consoles and grid terminals.</summary>
    public const string CONSOLE = "station.console";

    /// <summary>Alternate station tag for WAY-OS computer terminals.</summary>
    public const string COMPUTER = "station.computer";

    /// <summary>Station tag for dropped death-loot containers.</summary>
    public const string DEATH_CONTAINER = "station.death_container";

    /// <summary>Station tag for suit crafting self-actions.</summary>
    public const string SUIT = "suit";

    /// <summary>Returns the tag with any leading <c>station.</c> prefix removed.</summary>
    public static string NormalizeShortTag(string? stationTag)
    {
        if (string.IsNullOrWhiteSpace(stationTag))
        {
            return string.Empty;
        }

        string trimmed = stationTag.Trim();
        return trimmed.StartsWith(BrickTags.STATION_PREFIX, StringComparison.Ordinal)
            ? trimmed[BrickTags.STATION_PREFIX.Length..]
            : trimmed;
    }

    /// <summary>Resolves the default <see cref="StationUiKind"/> for a brick-entity from its tag and inventory presence.</summary>
    public static StationUiKind ResolveUiKind(string? stationTag, bool hasInventory)
    {
        string shortTag = NormalizeShortTag(stationTag);
        if (string.Equals(shortTag, "console", StringComparison.Ordinal)
            || string.Equals(shortTag, "computer", StringComparison.Ordinal))
        {
            return StationUiKind.Terminal;
        }

        if (!hasInventory
            && (string.Equals(shortTag, "door", StringComparison.Ordinal)
                || string.Equals(shortTag, "airlock", StringComparison.Ordinal)))
        {
            return StationUiKind.None;
        }

        if (shortTag.Length > 0 || hasInventory)
        {
            return StationUiKind.ContextualTab;
        }

        return StationUiKind.None;
    }
}

using System;

namespace WaywardBeyond.Gameplay.Power;

/// <summary>Deterministic brownout priority tiers resolved from a brick station tag.</summary>
public static class PowerGridPriority
{
    /// <summary>Station tag of the WAY-OS telemetry terminal.</summary>
    public const string STATION_CONSOLE = "station.console";

    /// <summary>Station tag of the life-support oxygen extractor.</summary>
    public const string STATION_O2_EXTRACTOR = "station.o2_extractor";

    /// <summary>Station tag of the refinery auto-processor.</summary>
    public const string STATION_REFINERY = "station.refinery";

    /// <summary>Station tag of the synthesizer multi-recipe crafter.</summary>
    public const string STATION_SYNTHESIZER = "station.synthesizer";

    /// <summary>Station tag of the composter auto-processor.</summary>
    public const string STATION_COMPOSTER = "station.composter";

    /// <summary>Station tag of the hydroponic bay auto-processor.</summary>
    public const string STATION_HYDROPONIC_BAY = "station.hydroponic_bay";

    /// <summary>Station tag of the oven multi-recipe crafter.</summary>
    public const string STATION_OVEN = "station.oven";

    /// <summary>Highest tier: life support and terminal nodes stay alive first.</summary>
    public const byte LIFE_SUPPORT = 0;

    /// <summary>Second tier: crafters and processors.</summary>
    public const byte PROCESSOR = 1;

    /// <summary>Lowest tier: anything without an explicit station priority.</summary>
    public const byte AUXILIARY = 2;

    /// <summary>Resolves the brownout tier for <paramref name="stationTag"/>; lower values keep power first.</summary>
    public static byte Resolve(string? stationTag)
    {
        if (string.IsNullOrEmpty(stationTag))
        {
            return AUXILIARY;
        }

        if (string.Equals(stationTag, STATION_CONSOLE, StringComparison.Ordinal)
            || string.Equals(stationTag, STATION_O2_EXTRACTOR, StringComparison.Ordinal))
        {
            return LIFE_SUPPORT;
        }

        if (string.Equals(stationTag, STATION_REFINERY, StringComparison.Ordinal)
            || string.Equals(stationTag, STATION_SYNTHESIZER, StringComparison.Ordinal)
            || string.Equals(stationTag, STATION_COMPOSTER, StringComparison.Ordinal)
            || string.Equals(stationTag, STATION_HYDROPONIC_BAY, StringComparison.Ordinal)
            || string.Equals(stationTag, STATION_OVEN, StringComparison.Ordinal))
        {
            return PROCESSOR;
        }

        return AUXILIARY;
    }
}

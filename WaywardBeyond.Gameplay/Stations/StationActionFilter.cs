using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;

namespace WaywardBeyond.Gameplay.Stations;

/// <summary>Optional pre-filter constraining which station commands an <see cref="IStationActionHandler"/> receives.</summary>
public readonly record struct StationActionFilter(
    StationActionKind? Action = null,
    string? ActionId = null,
    GameMode? GameMode = null
);

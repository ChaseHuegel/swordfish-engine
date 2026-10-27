using System.Collections.Generic;
using WaywardBeyond.Data;

namespace WaywardBeyond.Client.UI.Layers.Menus.Main;

/// <summary>Dynamic state for the galaxy archive page.</summary>
internal readonly record struct SelectSaveState(
    bool Remote,
    bool CanCreateSave,
    int Tab,
    bool HasActive,
    IReadOnlyList<SaveRowState> Saves,
    SaveDetailState Detail
);

/// <summary>Dynamic facts shown by one galaxy archive row.</summary>
internal readonly record struct SaveRowState(
    string Guid,
    string Name,
    GameMode Mode,
    bool Creative,
    long LastPlayedMs,
    bool Selected
);

/// <summary>Dynamic facts shown by the telemetry panel.</summary>
internal readonly record struct SaveDetailState(
    string Name,
    GameMode Mode,
    string Seed,
    long AgeMs,
    long LastPlayedMs
);

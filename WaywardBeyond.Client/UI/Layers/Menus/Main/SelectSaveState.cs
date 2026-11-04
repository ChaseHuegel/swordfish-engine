using System.Collections.Generic;
using WaywardBeyond.Data;

namespace WaywardBeyond.Client.UI.Layers.Menus.Main;

/// <summary>Dynamic state for the galaxy archive page.</summary>
internal readonly record struct SelectSaveState(
    bool Remote,
    bool CanCreateSave,
    int Tab,
    bool HasActive,
    IReadOnlyList<SaveRowState> Saves
);

/// <summary>Dynamic facts shown by one galaxy archive row.</summary>
internal readonly record struct SaveRowState(
    string Guid,
    string Name,
    GameMode Mode,
    bool Creative,
    long LastPlayedMs,
    bool Selected,
    long Seed,
    long AgeMs,
    string SeedPhrase
);

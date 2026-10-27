using System.Collections.Generic;
using Swordfish.Graphics;

namespace WaywardBeyond.Client.UI.Layers.Menus.Main;

/// <summary>Dynamic state for the crew manifest page.</summary>
internal readonly record struct SelectCharacterState(
    bool HasCharacters,
    bool HasActive,
    bool CanLaunch,
    int Tab,
    IReadOnlyList<CharacterRowState> Characters,
    CharacterDetailState Detail
);

/// <summary>Dynamic facts shown by one crew manifest row.</summary>
internal readonly record struct CharacterRowState(
    string Uuid,
    string Name,
    bool Selected,
    Material Portrait
);

/// <summary>Dynamic facts shown by the crew detail panel.</summary>
internal readonly record struct CharacterDetailState(
    string Name,
    int Level,
    string Species,
    long LastPlayedMs,
    long AgeMs,
    Material Portrait,
    IReadOnlyList<StatRowState> Stats,
    string? Assignment
);

/// <summary>One stat label key and value.</summary>
internal readonly record struct StatRowState(string LabelKey, string Value);

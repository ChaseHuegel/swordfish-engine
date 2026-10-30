using System.Collections.Generic;
using Swordfish.Graphics;

namespace WaywardBeyond.Client.UI.Layers.Menus.Main;

/// <summary>Dynamic state for the crew manifest page.</summary>
internal readonly record struct SelectCharacterState(
    bool HasCharacters,
    bool HasActive,
    bool CanLaunch,
    bool ShowAssignment,
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
    string LevelLine,
    string LastPlayed,
    string TimePlayed,
    Material Portrait,
    IReadOnlyList<StatRowState> Stats,
    string Assignment
);

/// <summary>One stat name and value.</summary>
internal readonly record struct StatRowState(string Label, string Value);

using System.Collections.Generic;
using Swordfish.Graphics;

namespace WaywardBeyond.Client.UI.Layers.Menus.Main;

/// <summary>Dynamic state for the colonist form page.</summary>
internal readonly record struct NewCharacterState(
    int Tab,
    int SpacerPoints,
    Material Appearance,
    SelectorState Species,
    SelectorState Gender,
    IReadOnlyList<CharacterStatRow> Stats,
    string InfoLabel,
    string InfoBody,
    string? Status,
    bool StatusDanger,
    bool StatusWarning,
    bool CanCreate
);

/// <summary>One labeled discrete selector.</summary>
internal readonly record struct SelectorState(
    string Label,
    string Value,
    bool Enabled
);

/// <summary>One spacer stat row.</summary>
internal readonly record struct CharacterStatRow(
    string Label,
    string Value,
    bool Focused,
    bool CanDecrease,
    bool CanIncrease
);

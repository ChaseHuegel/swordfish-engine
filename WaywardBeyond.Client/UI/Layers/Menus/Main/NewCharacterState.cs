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
    string InfoStatLabelKey,
    string InfoStatKey,
    string StatusKey,
    string StatusValue,
    bool StatusDanger,
    bool StatusWarning,
    bool CanCreate
);

/// <summary>One labeled discrete selector.</summary>
internal readonly record struct SelectorState(
    string Value,
    bool Enabled,
    bool CanDecrease,
    bool CanIncrease
);

/// <summary>One spacer stat row.</summary>
internal readonly record struct CharacterStatRow(
    string LabelKey,
    string Value,
    bool Focused,
    bool CanDecrease,
    bool CanIncrease
);

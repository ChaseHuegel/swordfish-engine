using System.Collections.Generic;

namespace WaywardBeyond.Client.UI.Layers.Menus.Main;

/// <summary>Dynamic state for the galaxy form and its creation overlay.</summary>
internal readonly record struct NewSaveState(
    bool Creating,
    string Name,
    string ModeLine,
    string SeedLine,
    string TypeLine,
    bool CanCreate,
    string? Status,
    bool StatusDanger,
    bool StatusWarning,
    int Tab,
    IReadOnlyList<CreateLineState> Lines,
    bool CursorOn
);

/// <summary>One fading line of the creation log.</summary>
internal readonly record struct CreateLineState(string Text, float Fade);

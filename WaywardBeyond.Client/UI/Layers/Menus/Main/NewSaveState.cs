using System.Collections.Generic;

namespace WaywardBeyond.Client.UI.Layers.Menus.Main;

/// <summary>Dynamic state for the galaxy form and its creation overlay.</summary>
internal readonly record struct NewSaveState(
    bool Creating,
    string Name,
    string Seed,
    bool CanCreate,
    string StatusKey,
    bool StatusDanger,
    bool StatusWarning,
    int Tab,
    IReadOnlyList<CreateLineState> Lines,
    double CreateElapsed
);

/// <summary>One line of the creation log and its age.</summary>
internal readonly record struct CreateLineState(string Text, double Age);

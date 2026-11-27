using System.Collections.Generic;
using WaywardBeyond.Data;

namespace WaywardBeyond.Client.UI.Layers.Menus.Main;

/// <summary>Dynamic state for the galaxy form and its creation overlay.</summary>
internal readonly record struct NewSaveState(
    bool Creating,
    string Name,
    string Seed,
    GameMode Mode,
    bool CanCreate,
    string StatusKey,
    bool StatusDanger,
    bool StatusWarning,
    int Tab,
    IReadOnlyList<CreateLineState> Lines,
    double CreateElapsed
);

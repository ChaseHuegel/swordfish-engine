using System;
using System.Collections.Generic;
using Swordfish.Graphics;

namespace WaywardBeyond.Client.UI.Layers;

/// <summary>One node of a radial selector.</summary>
internal readonly record struct RadialNodeState(
    string Id,
    int Index,
    int X,
    int Y,
    Material Icon,
    bool Selected
);

/// <summary>Display data for a radial selector.</summary>
internal readonly record struct RadialMenuState(
    string Label,
    IReadOnlyList<RadialNodeState> Nodes,
    Action<int>? OnEnter
);

/// <summary>Draws radial selector nodes and the selected label.</summary>
internal partial class RadialMenuView
{
}

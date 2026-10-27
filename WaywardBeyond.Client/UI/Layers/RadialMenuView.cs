using System;
using System.Collections.Generic;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;

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
    string LabelKey,
    IReadOnlyList<RadialNodeState> Nodes,
    Action<int>? OnEnter
);

/// <summary>Draws radial selector nodes and the selected label.</summary>
internal partial class RadialMenuView(ILocalization localization)
{
    /// <summary>Returns a localized string, or the key when the string is missing.</summary>
    protected string L(string key) => localization.GetString(key) ?? key;
}

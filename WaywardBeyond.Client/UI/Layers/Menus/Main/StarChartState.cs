using System;
using System.Collections.Generic;
using Swordfish.Library.Globalization;
using WaywardBeyond.Client.UI.Components;

namespace WaywardBeyond.Client.UI.Layers.Menus.Main;

/// <summary>Context for the star chart component.</summary>
internal readonly record struct StarChartState(
    IReadOnlyList<StarChartNodeState> Nodes,
    IReadOnlyList<StarSpeck> Specks,
    ILocalization Localization,
    Action<string> OnSelect,
    bool Ready
)
{
    private static readonly string DiamondGlyph = new IconParameters(IconGlyph.Diamond).Text;

    /// <summary>Returns a localized string, or the key when the string is missing.</summary>
    public string Text(string key)
    {
        return Localization.GetString(key) ?? key;
    }

    /// <summary>Gets the diamond glyph from the shared icon set.</summary>
    public string Glyph => DiamondGlyph;

    /// <summary>Gets whether the chart has a measured field and no saves to plot.</summary>
    public bool IsEmpty => Ready && Nodes.Count == 0;
}

/// <summary>One save as the star chart draws it.</summary>
internal readonly record struct StarChartNodeState(
    string Guid,
    string NodeId,
    string RowId,
    string Name,
    string Mode,
    string Seed,
    string TimePlayed,
    string LastPlayed,
    int ClassIndex,
    bool IsRed,
    bool IsEmerald,
    bool IsViolet,
    int X,
    int Y,
    int HitX,
    int HitY,
    int GlyphX,
    int GlyphY,
    int BracketLeftX,
    int BracketRightX,
    int BracketY,
    int LabelX,
    int LabelY,
    int CardX,
    int CardY,
    int CardHeight,
    bool Selected,
    bool Passthrough
);

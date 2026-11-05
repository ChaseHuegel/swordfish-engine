using System;
using System.Collections.Generic;
using System.Text;
using Reef;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using WaywardBeyond.Client.Extensions;
using WaywardBeyond.Client.UI.Components;
using WaywardBeyond.Data;

namespace WaywardBeyond.Client.UI.Layers.Menus.Main;

/// <summary>Forwards galaxy archive interactions and formats archive values.</summary>
internal partial class SelectSaveView : UiViewBase
{
    private readonly ILocalization _localization;

    private readonly Dictionary<string, (int Width, int Height)> _labelSizes = new(StringComparer.Ordinal);
    private string _chartKey = string.Empty;
    private StarChartGeometry _chartGeometry = StarChartGeometry.Empty;
    private StarChartNodeState[] _chartNodes = Array.Empty<StarChartNodeState>();

    /// <summary>Initializes the galaxy archive view.</summary>
    public SelectSaveView(ILocalization localization, ScrollAreaState scroll) : base(localization)
    {
        _localization = localization;
        Scroll = scroll;
    }

    /// <summary>Gets the archive list scroll state.</summary>
    public ScrollAreaState Scroll { get; }

    /// <summary>Raised when an archive row is selected.</summary>
    public Action<string>? SaveSelected { get; set; }

    /// <summary>Raised when an archive row asks to be deleted.</summary>
    public Action<string>? SaveDeleteRequested { get; set; }

    /// <summary>Raised when the new archive button is clicked.</summary>
    public Action? NewSaveRequested { get; set; }

    /// <summary>Raised when a narrow layout tab is clicked.</summary>
    public Action<int>? TabRequested { get; set; }

    /// <summary>Raised when back is clicked.</summary>
    public Action? BackRequested { get; set; }

    /// <summary>Raised when the select button is clicked.</summary>
    public Action? NextRequested { get; set; }

    partial void OnSelectSave(string guid) => SaveSelected?.Invoke(guid);

    partial void OnDeleteSave(string guid) => SaveDeleteRequested?.Invoke(guid);

    partial void OnNewSave() => NewSaveRequested?.Invoke();

    partial void OnSelectTab(int tab) => TabRequested?.Invoke(tab);

    partial void OnBack() => BackRequested?.Invoke();

    partial void OnNext() => NextRequested?.Invoke();

    /// <summary>Formats the last played line of an archive row.</summary>
    public string LastPlayedLine(long lastPlayedMs)
    {
        return L("ui.label.lastPlayed") + ": " + LastPlayedText(lastPlayedMs);
    }

    /// <summary>Formats the last played stamp, or the never label when the save was not played.</summary>
    public string LastPlayedText(long lastPlayedMs)
    {
        return lastPlayedMs > 0
            ? DateTimeOffset.FromUnixTimeMilliseconds(lastPlayedMs).ToLocalTime().ToString("g")
            : L("ui.label.never");
    }

    /// <summary>Formats the accumulated playtime.</summary>
    public string TimePlayedText(long ageMs)
    {
        return _localization.GetLongString(TimeSpan.FromMilliseconds(ageMs));
    }

    /// <summary>Gets the short game mode label shown on an archive row.</summary>
    public static string ModeTag(GameMode mode)
    {
        return mode.ToString().ToUpperInvariant();
    }

    /// <summary>Gets the display name of the game mode.</summary>
    public static string ModeName(GameMode mode)
    {
        return mode.ToString();
    }

    /// <summary>Records the measured size of each chart label from the previous frame.</summary>
    public void Measure(UIBuilder<Material> ui, SelectSaveState state)
    {
        foreach (SaveRowState row in state.Saves)
        {
            ElementBounds bounds = ui.GetElementBounds("SelectSave.LabelText." + row.Guid);
            if (bounds.Rect.Size.X > 0 && bounds.Rect.Size.Y > 0)
            {
                _labelSizes[row.Guid] = (bounds.Rect.Size.X, bounds.Rect.Size.Y);
            }
        }
    }

    /// <summary>Builds the star chart context for a field size, reusing cached geometry.</summary>
    public StarChartState Chart(SelectSaveState state, int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            return new StarChartState(
                Array.Empty<StarChartNodeState>(),
                Array.Empty<StarSpeck>(),
                _localization,
                SelectNode,
                Ready: false
            );
        }

        string key = ChartKey(state, width, height);
        if (!string.Equals(key, _chartKey, StringComparison.Ordinal))
        {
            _chartKey = key;
            _chartGeometry = BuildGeometry(state, width, height);
            _chartNodes = BuildNodes(state, _chartGeometry);
        }

        return new StarChartState(
            _chartNodes,
            _chartGeometry.Specks,
            _localization,
            SelectNode,
            Ready: true
        );
    }

    /// <summary>Computes the chart geometry for the current save set and field size.</summary>
    private StarChartGeometry BuildGeometry(in SelectSaveState state, int width, int height)    {
        var sources = new List<StarChartSource>(state.Saves.Count);
        string? selected = null;

        foreach (SaveRowState row in state.Saves)
        {
            (int labelWidth, int labelHeight) = _labelSizes.TryGetValue(row.Guid, out (int, int) size)
                ? size
                : (0, 0);

            sources.Add(new StarChartSource(row.Guid, row.Seed, labelWidth, labelHeight));
            if (row.Selected)
            {
                selected = row.Guid;
            }
        }

        return StarChartLayout.Compute(sources, selected, width, height);
    }

    /// <summary>Joins chart positions with the display facts of each save.</summary>
    private StarChartNodeState[] BuildNodes(in SelectSaveState state, in StarChartGeometry geometry)
    {
        var byGuid = new Dictionary<string, SaveRowState>(state.Saves.Count, StringComparer.Ordinal);
        foreach (SaveRowState row in state.Saves)
        {
            byGuid[row.Guid] = row;
        }

        var nodes = new StarChartNodeState[geometry.Nodes.Count];
        for (var i = 0; i < geometry.Nodes.Count; i++)
        {
            StarNodePlacement placement = geometry.Nodes[i];
            if (!byGuid.TryGetValue(placement.Guid, out SaveRowState row))
            {
                continue;
            }

            int half = StarChartLayout.HIT_SIZE / 2;
            int glyph = StarChartLayout.GLYPH_SIZE / 2;

            nodes[i] = new StarChartNodeState(
                Guid: row.Guid,
                NodeId: "SelectSave.Node." + row.Guid,
                RowId: "Button_SelectSave_" + row.Name,
                Name: row.Name,
                Mode: ModeName(row.Mode),
                Seed: row.SeedPhrase,
                TimePlayed: TimePlayedText(row.AgeMs),
                LastPlayed: LastPlayedText(row.LastPlayedMs),
                ClassIndex: placement.ClassIndex,
                IsRed: placement.ClassIndex == 1,
                IsEmerald: placement.ClassIndex == 2,
                IsViolet: placement.ClassIndex == 3,
                X: placement.X,
                Y: placement.Y,
                HitX: placement.X - half,
                HitY: placement.Y - half,
                GlyphX: placement.X - glyph,
                GlyphY: placement.Y - glyph,
                BracketLeftX: placement.X + StarChartLayout.BRACKET_OFFSET_LEFT,
                BracketRightX: placement.X + StarChartLayout.BRACKET_OFFSET_RIGHT,
                BracketY: placement.Y + StarChartLayout.BRACKET_OFFSET_Y,
                LabelX: placement.LabelX,
                LabelY: placement.LabelY,
                CardX: placement.CardX,
                CardY: placement.CardY,
                CardHeight: StarChartLayout.CARD_HEIGHT,
                LabelWidth: placement.LabelWidth,
                LabelHeight: placement.LabelHeight,
                Selected: placement.Selected,
                Passthrough: placement.Passthrough
            );
        }

        return nodes;
    }

    /// <summary>Builds the cache key for the chart geometry.</summary>
    private string ChartKey(in SelectSaveState state, int width, int height)
    {
        var builder = new StringBuilder(state.Saves.Count * 32);
        builder.Append(width).Append('x').Append(height);

        foreach (SaveRowState row in state.Saves)
        {
            builder.Append('|').Append(row.Guid).Append(',').Append(row.Seed);
            if (row.Selected)
            {
                builder.Append(',');
            }

            //  Measured label size feeds the packing, so a change repacks.
            if (TryGetSize(state, row.Guid, out int labelWidth, out int labelHeight))
            {
                builder.Append('@').Append(labelWidth).Append('x').Append(labelHeight);
            }
        }

        return builder.ToString();
    }

    /// <summary>Gets the measured label size for one save, when it is known.</summary>
    private bool TryGetSize(in SelectSaveState state, string guid, out int width, out int height)
    {
        if (_labelSizes.TryGetValue(guid, out (int Width, int Height) size))
        {
            width = size.Width;
            height = size.Height;
            return true;
        }

        width = 0;
        height = 0;
        return false;
    }

    /// <summary>Raises the save selection for a chart node.</summary>
    private void SelectNode(string guid)
    {
        SaveSelected?.Invoke(guid);
    }
}

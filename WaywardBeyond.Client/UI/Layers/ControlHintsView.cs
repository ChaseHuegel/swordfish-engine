using System.Collections.Generic;

namespace WaywardBeyond.Client.UI.Layers;

/// <summary>One keycap and action hint.</summary>
internal readonly record struct HintViewState(string Key, string Action)
{
    /// <summary>Gets whether the hint has a keycap.</summary>
    public bool HasKey => Key.Length > 0;
}

/// <summary>Display data for the control hints.</summary>
internal readonly record struct ControlHintsState(IReadOnlyList<HintViewState> Hints);

/// <summary>Draws contextual keycap hints.</summary>
internal partial class ControlHintsView
{
}

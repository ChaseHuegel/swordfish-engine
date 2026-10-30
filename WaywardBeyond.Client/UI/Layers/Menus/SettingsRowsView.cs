using System;
using System.Collections.Generic;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using WaywardBeyond.Client.UI.Components;
using WaywardBeyond.Client.UI.Settings;

namespace WaywardBeyond.Client.UI.Layers.Menus;

/// <summary>Forwards settings row interactions.</summary>
internal partial class SettingsRowsView(ILocalization localization) : UiViewBase(localization)
{
    /// <summary>Gets the rows scroll state.</summary>
    public ScrollAreaState Scroll { get; } = new();

    /// <summary>Raised when a choice cell is clicked.</summary>
    public Action<string, int>? ChoiceRequested { get; set; }

    /// <summary>Raised when a stepper moves.</summary>
    public Action<string, int>? StepRequested { get; set; }

    /// <summary>Raised when a slider changes.</summary>
    public Action<string, float>? SliderRequested { get; set; }

    /// <summary>Raised when a reset row is clicked.</summary>
    public Action<string>? ResetRequested { get; set; }

    partial void OnStep(string id, int delta) => StepRequested?.Invoke(id, delta);

    /// <summary>Reports a choice change to the hosting page.</summary>
    public void OnChoiceChanged(string id, int index) => ChoiceRequested?.Invoke(id, index);

    partial void OnReset(string id) => ResetRequested?.Invoke(id);

    /// <summary>Reports a slider change to the hosting page.</summary>
    public void OnSliderChanged(string id, float value) => SliderRequested?.Invoke(id, value);
}

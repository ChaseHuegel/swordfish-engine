using System;
using System.Collections.Generic;
using WaywardBeyond.Client.UI.Components;
using Reef;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using WaywardBeyond.Client.UI.Settings;

namespace WaywardBeyond.Client.UI.Layers.Menus;

/// <summary>Forwards settings shell interactions and supplies the pane body.</summary></summary>
internal partial class SettingsShellView(ILocalization localization) : UiViewBase(localization)
{
    /// <summary>Gets the row list scroll state.</summary>
    public ScrollAreaState Scroll { get; } = new();

    /// <summary>Gets or sets the rows rendered inside the pane.</summary>
    public IReadOnlyList<SettingsRowState> Rows { get; set; } = [];

    /// <summary>Gets or sets the control column width.</summary>
    public int ControlWidth { get; set; } = 220;

    /// <summary>Gets or sets the reset row label.</summary>
    public string ResetLabel { get; set; } = string.Empty;

    /// <summary>Gets the choice callback the control widget receives.</summary>
    public Action<string, int>? ChoiceChanged => ChoiceRequested;

    /// <summary>Gets the slider callback the control widget receives.</summary>
    public Action<string, float>? SliderChanged => SliderRequested;

    /// <summary>Forwarded when a choice changes.</summary>
    public Action<string, int>? ChoiceRequested { get; set; }

    /// <summary>Forwarded when a stepper moves.</summary>
    public Action<string, int>? StepRequested { get; set; }

    /// <summary>Forwarded when a slider changes.</summary>
    public Action<string, float>? SliderRequested { get; set; }

    /// <summary>Forwarded when a reset row is clicked.</summary>
    public Action<string>? ResetRequested { get; set; }

    /// <summary>Raised when a category is selected.</summary>
    public Action<int>? CategoryRequested { get; set; }

    /// <summary>Raised when cancel or back is clicked.</summary>
    public Action? CancelRequested { get; set; }

    /// <summary>Raised when apply is clicked.</summary>
    public Action? ApplyRequested { get; set; }

    partial void OnSelectCategory(int index) => CategoryRequested?.Invoke(index);

    partial void OnCancel() => CancelRequested?.Invoke();

    partial void OnApply() => ApplyRequested?.Invoke();

    partial void OnStep(string id, int delta) => StepRequested?.Invoke(id, delta);

    partial void OnReset(string id) => ResetRequested?.Invoke(id);
}

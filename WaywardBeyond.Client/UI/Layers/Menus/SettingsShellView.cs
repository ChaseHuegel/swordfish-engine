using System;
using Reef;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using WaywardBeyond.Client.UI.Settings;

namespace WaywardBeyond.Client.UI.Layers.Menus;

/// <summary>Forwards settings shell interactions and supplies the pane body.</summary></summary>
internal partial class SettingsShellView(ILocalization localization) : UiViewBase(localization)
{
    /// <summary>Gets or sets the pane body drawn inside the shell.</summary>
    public Action<UIBuilder<Material>>? Body { get; set; }

    /// <summary>Raised when a category is selected.</summary>
    public Action<int>? CategoryRequested { get; set; }

    /// <summary>Raised when cancel or back is clicked.</summary>
    public Action? CancelRequested { get; set; }

    /// <summary>Raised when apply is clicked.</summary>
    public Action? ApplyRequested { get; set; }

    partial void OnSelectCategory(int index) => CategoryRequested?.Invoke(index);

    partial void OnCancel() => CancelRequested?.Invoke();

    partial void OnApply() => ApplyRequested?.Invoke();
}

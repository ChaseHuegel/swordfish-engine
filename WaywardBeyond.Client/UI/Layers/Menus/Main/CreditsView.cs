using System;
using Swordfish.Library.Globalization;
using WaywardBeyond.Client.UI.Components;

namespace WaywardBeyond.Client.UI.Layers.Menus.Main;

/// <summary>Forwards credits interactions to the hosting page.</summary>
internal partial class CreditsView : UiViewBase
{
    /// <summary>Initializes the credits view.</summary>
    public CreditsView(ILocalization localization, ScrollAreaState scroll) : base(localization)
    {
        Scroll = scroll;
    }

    /// <summary>The credits scroll state.</summary>
    public ScrollAreaState Scroll { get; }

    /// <summary>Raised when the back action is clicked.</summary>
    public Action? BackRequested { get; set; }

    partial void OnBack() => BackRequested?.Invoke();
}

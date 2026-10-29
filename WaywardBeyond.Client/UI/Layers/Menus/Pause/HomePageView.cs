using System;
using Swordfish.Library.Globalization;

namespace WaywardBeyond.Client.UI.Layers.Menus.Pause;

/// <summary>Forwards pause menu interactions to the hosting page.</summary>
internal partial class HomePageView : UiViewBase
{
    /// <summary>Initializes the pause home view.</summary>
    public HomePageView(ILocalization localization) : base(localization)
    {
    }

    /// <summary>Raised when Continue is clicked.</summary>
    public Action? ContinueRequested { get; set; }

    /// <summary>Raised when Settings is clicked.</summary>
    public Action? SettingsRequested { get; set; }

    /// <summary>Raised when Feedback is clicked.</summary>
    public Action? FeedbackRequested { get; set; }

    /// <summary>Raised when Save and exit is clicked.</summary>
    public Action? SaveAndExitRequested { get; set; }

    partial void OnContinue() => ContinueRequested?.Invoke();

    partial void OnSettings() => SettingsRequested?.Invoke();

    partial void OnFeedback() => FeedbackRequested?.Invoke();

    partial void OnSaveAndExit() => SaveAndExitRequested?.Invoke();
}

using System;
using Swordfish.Library.Globalization;

namespace WaywardBeyond.Client.UI.Layers.Menus.Main;

/// <summary>Forwards main menu overlay actions to the hosting menu.</summary>
internal partial class MainMenuOverlayView : UiViewBase
{
    /// <summary>Initializes the main menu overlay.</summary>
    public MainMenuOverlayView(ILocalization localization) : base(localization)
    {
    }

    /// <summary>Raised when feedback is clicked.</summary>
    public Action? FeedbackRequested { get; set; }

    /// <summary>Raised when Discord is clicked.</summary>
    public Action? DiscordRequested { get; set; }

    /// <summary>Raised when Steam is clicked.</summary>
    public Action? SteamRequested { get; set; }

    partial void OnFeedback() => FeedbackRequested?.Invoke();

    partial void OnDiscord() => DiscordRequested?.Invoke();

    partial void OnSteam() => SteamRequested?.Invoke();
}

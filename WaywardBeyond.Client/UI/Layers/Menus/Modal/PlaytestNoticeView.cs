using System;
using Swordfish.Library.Globalization;

namespace WaywardBeyond.Client.UI.Layers.Menus.Modal;

/// <summary>Forwards playtest notice interactions to the hosting menu page.</summary>
internal partial class PlaytestNoticeView : UiViewBase
{
    /// <summary>Initializes the playtest notice view.</summary>
    public PlaytestNoticeView(ILocalization localization) : base(localization)
    {
    }

    /// <summary>Raised when the close action is clicked.</summary>
    public Action? CloseRequested { get; set; }

    /// <summary>Raised when the Discord action is clicked.</summary>
    public Action? DiscordRequested { get; set; }

    /// <summary>Raised when the Steam action is clicked.</summary>
    public Action? SteamRequested { get; set; }

    partial void OnClose() => CloseRequested?.Invoke();

    partial void OnDiscord() => DiscordRequested?.Invoke();

    partial void OnSteam() => SteamRequested?.Invoke();
}

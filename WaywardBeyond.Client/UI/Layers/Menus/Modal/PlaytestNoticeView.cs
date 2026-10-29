using System;

namespace WaywardBeyond.Client.UI.Layers.Menus.Modal;

/// <summary>Forwards playtest notice interactions to the hosting menu page.</summary>
internal partial class PlaytestNoticeView
{
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

using System;
using Swordfish.Library.Globalization;
using WaywardBeyond.Client.Extensions;
using WaywardBeyond.Data;

namespace WaywardBeyond.Client.UI.Layers.Menus.Main;

/// <summary>Forwards home menu interactions to the hosting page and formats Continue card values.</summary>
internal partial class HomeView : UiViewBase
{
    private readonly ILocalization _localization;

    /// <summary>Initializes the home menu view.</summary>
    public HomeView(ILocalization localization) : base(localization)
    {
        _localization = localization;
    }

    /// <summary>Raised when Continue is clicked.</summary>
    public Action? ContinueRequested { get; set; }

    /// <summary>Raised when Singleplayer is clicked.</summary>
    public Action? SingleplayerRequested { get; set; }

    /// <summary>Raised when Multiplayer is clicked.</summary>
    public Action? MultiplayerRequested { get; set; }

    /// <summary>Raised when Settings is clicked.</summary>
    public Action? SettingsRequested { get; set; }

    /// <summary>Raised when Credits is clicked.</summary>
    public Action? CreditsRequested { get; set; }

    /// <summary>Raised when Quit is clicked.</summary>
    public Action? QuitRequested { get; set; }

    partial void OnContinue() => ContinueRequested?.Invoke();

    partial void OnSingleplayer() => SingleplayerRequested?.Invoke();

    partial void OnMultiplayer() => MultiplayerRequested?.Invoke();

    partial void OnSettings() => SettingsRequested?.Invoke();

    partial void OnCredits() => CreditsRequested?.Invoke();

    partial void OnQuit() => QuitRequested?.Invoke();

    /// <summary>Formats the last played stamp, or the never label when the save was not played.</summary>
    protected string LastPlayedText(long lastPlayedMs)
    {
        return lastPlayedMs > 0
            ? DateTimeOffset.FromUnixTimeMilliseconds(lastPlayedMs).ToLocalTime().ToString("d")
            : L("ui.label.never");
    }

    /// <summary>Formats the accumulated playtime.</summary>
    protected string TimePlayedText(long ageMs)
    {
        return _localization.GetLongString(TimeSpan.FromMilliseconds(ageMs));
    }

    /// <summary>Formats the colonist level and species line.</summary>
    protected string LevelLine(int level, string species)
    {
        return _localization.GetCharacterLevelLine(level, species);
    }

    /// <summary>Gets the game mode name shown in the Continue card.</summary>
    protected static string ModeName(GameMode mode)
    {
        return mode.ToString();
    }
}

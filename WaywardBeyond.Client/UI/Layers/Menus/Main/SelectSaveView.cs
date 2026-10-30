using System;
using Swordfish.Library.Globalization;
using WaywardBeyond.Client.Extensions;
using WaywardBeyond.Client.UI.Components;
using WaywardBeyond.Data;

namespace WaywardBeyond.Client.UI.Layers.Menus.Main;

/// <summary>Forwards galaxy archive interactions and formats archive values.</summary>
internal partial class SelectSaveView : UiViewBase
{
    private readonly ILocalization _localization;

    /// <summary>Initializes the galaxy archive view.</summary>
    public SelectSaveView(ILocalization localization, ScrollAreaState scroll) : base(localization)
    {
        _localization = localization;
        Scroll = scroll;
    }

    /// <summary>Gets the archive list scroll state.</summary>
    public ScrollAreaState Scroll { get; }

    /// <summary>Raised when an archive row is selected.</summary>
    public Action<string>? SaveSelected { get; set; }

    /// <summary>Raised when an archive row asks to be deleted.</summary>
    public Action<string>? SaveDeleteRequested { get; set; }

    /// <summary>Raised when the new archive button is clicked.</summary>
    public Action? NewSaveRequested { get; set; }

    /// <summary>Raised when a narrow layout tab is clicked.</summary>
    public Action<int>? TabRequested { get; set; }

    /// <summary>Raised when back is clicked.</summary>
    public Action? BackRequested { get; set; }

    /// <summary>Raised when the select button is clicked.</summary>
    public Action? NextRequested { get; set; }

    partial void OnSelectSave(string guid) => SaveSelected?.Invoke(guid);

    partial void OnDeleteSave(string guid) => SaveDeleteRequested?.Invoke(guid);

    partial void OnNewSave() => NewSaveRequested?.Invoke();

    partial void OnSelectTab(int tab) => TabRequested?.Invoke(tab);

    partial void OnBack() => BackRequested?.Invoke();

    partial void OnNext() => NextRequested?.Invoke();

    /// <summary>Formats the last played label line of an archive row.</summary>
    /// <summary>Formats the last played stamp, or the never label when the save was not played.</summary>
    public string LastPlayedLine(long lastPlayedMs)
    {
        return L("ui.label.lastPlayed") + ": " + LastPlayedText(lastPlayedMs);
    }

    public string LastPlayedText(long lastPlayedMs)
    {
        return lastPlayedMs > 0
            ? DateTimeOffset.FromUnixTimeMilliseconds(lastPlayedMs).ToLocalTime().ToString("g")
            : L("ui.label.never");
    }

    /// <summary>Formats the accumulated playtime.</summary>
    public string TimePlayedText(long ageMs)
    {
        return _localization.GetLongString(TimeSpan.FromMilliseconds(ageMs));
    }

    /// <summary>Gets the short game mode label shown on an archive row.</summary>
    public static string ModeTag(GameMode mode)
    {
        return mode.ToString().ToUpperInvariant();
    }
}

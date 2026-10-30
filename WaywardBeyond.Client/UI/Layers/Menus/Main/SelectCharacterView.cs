using System;
using Swordfish.Library.Globalization;
using WaywardBeyond.Client.Extensions;
using WaywardBeyond.Client.UI.Components;

namespace WaywardBeyond.Client.UI.Layers.Menus.Main;

/// <summary>Forwards crew manifest interactions and formats crew values.</summary>
internal partial class SelectCharacterView : UiViewBase
{
    private readonly ILocalization _localization;

    /// <summary>Initializes the crew manifest view.</summary>
    public SelectCharacterView(ILocalization localization, ScrollAreaState scroll) : base(localization)
    {
        _localization = localization;
        Scroll = scroll;
    }

    /// <summary>Gets the manifest list scroll state.</summary>
    public ScrollAreaState Scroll { get; }

    /// <summary>Raised when a crew row is selected.</summary>
    public Action<string>? CharacterSelected { get; set; }

    /// <summary>Raised when a crew row asks to be deleted.</summary>
    public Action<string>? CharacterDeleteRequested { get; set; }

    /// <summary>Raised when the new colonist button is clicked.</summary>
    public Action? NewCharacterRequested { get; set; }

    /// <summary>Raised when a narrow layout tab is clicked.</summary>
    public Action<int>? TabRequested { get; set; }

    /// <summary>Raised when back is clicked.</summary>
    public Action? BackRequested { get; set; }

    /// <summary>Raised when launch is clicked.</summary>
    public Action? LaunchRequested { get; set; }

    partial void OnSelectCharacter(string uuid) => CharacterSelected?.Invoke(uuid);

    partial void OnDeleteCharacter(string uuid) => CharacterDeleteRequested?.Invoke(uuid);

    partial void OnNewCharacter() => NewCharacterRequested?.Invoke();

    partial void OnSelectTab(int tab) => TabRequested?.Invoke(tab);

    partial void OnBack() => BackRequested?.Invoke();

    partial void OnLaunch() => LaunchRequested?.Invoke();

    /// <summary>Formats the colonist level and species line.</summary>
    public string LevelLine(int level, string species)
    {
        return _localization.GetCharacterLevelLine(level, species);
    }

    /// <summary>Formats a labeled stamp such as last played or time played.</summary>
    public string Labeled(string labelKey, string value)
    {
        return L(labelKey) + ": " + value;
    }

    /// <summary>Formats the last played stamp, or the never label when the colonist was not played.</summary>
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

    /// <summary>Emphasizes the first letter of a stat name.</summary>
    public static string StatText(string label)
    {
        return label.Length == 0 ? label : $"#cyan{label[0]}#R{label[1..]}";
    }
}

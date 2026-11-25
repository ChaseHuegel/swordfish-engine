using System.Collections.Generic;

namespace WaywardBeyond.Client.UI.Settings;

/// <summary>Dynamic state for the settings page shell.</summary>
internal readonly record struct SettingsShellState(
    IReadOnlyList<SettingsCategoryEntry> Categories,
    int CategoryIndex,
    string TitleKey,
    bool Dirty,
    string StatusKey,
    string CancelLabelKey,
    bool CanApply = false,
    bool Capturing = false,
    bool StatusError = false
);

/// <summary>One category shown in the tray or tab strip.</summary>
internal readonly record struct SettingsCategoryEntry(string Id, string LabelKey);

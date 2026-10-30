using System.Collections.Generic;

namespace WaywardBeyond.Client.UI.Settings;

/// <summary>Dynamic state for the settings page shell.</summary>
internal readonly record struct SettingsShellState(
    bool UseTabs,
    IReadOnlyList<SettingsCategoryEntry> Categories,
    int CategoryIndex,
    string Title,
    bool Dirty,
    string? Status,
    string CancelLabel
);

/// <summary>One category shown in the tray or tab strip.</summary>
internal readonly record struct SettingsCategoryEntry(string Id, string Label);

using System.Collections.Generic;
using WaywardBeyond.Client.UI.Settings;

namespace WaywardBeyond.Client.UI.Layers.Menus;

/// <summary>Dynamic state for the settings row list.</summary>
internal readonly record struct SettingsRowsState(
    IReadOnlyList<SettingsRowState> Rows,
    int ControlWidth
);

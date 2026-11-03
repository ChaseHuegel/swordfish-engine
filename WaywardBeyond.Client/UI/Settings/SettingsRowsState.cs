using System;
using System.Collections.Generic;
using Swordfish.Library.Globalization;

namespace WaywardBeyond.Client.UI.Settings;

/// <summary>Context for the settings row list.</summary>
internal readonly record struct SettingsRowsState(
    IReadOnlyList<SettingsRowState> Rows,
    ILocalization Localization,
    Action<string, int>? OnChoice,
    Action<string, int>? OnStep,
    Action<string, float>? OnDrag,
    Action<string>? OnReset
)
{
    /// <summary>Returns a localized string, or the key when the string is missing.</summary>
    public string Text(string key)
    {
        return Localization.GetString(key) ?? key;
    }

    /// <summary>Returns the localized label of a row.</summary>
    public string Label(SettingsRowState row)
    {
        return Text(row.LabelKey);
    }

    /// <summary>Formats a stepper row value, resolving its key when it has one.</summary>
    public string StepperText(SettingsRowState row)
    {
        return row.ValueKey.Length > 0
            ? Localization.GetString(row.ValueKey) ?? row.ValueKey
            : row.Value;
    }

    /// <summary>Resolves a list of localization keys.</summary>
    public string[] LocalizeAll(string[] keys)
    {
        var labels = new string[keys.Length];
        for (var i = 0; i < keys.Length; i++)
        {
            labels[i] = Localization.GetString(keys[i]) ?? keys[i];
        }

        return labels;
    }
}

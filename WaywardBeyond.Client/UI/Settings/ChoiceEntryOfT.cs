using System;
using Swordfish.Library.Globalization;
using System.Collections.Generic;

namespace WaywardBeyond.Client.UI.Settings;

/// <summary>A setting rendered as a row of radio-like choices.</summary>
internal sealed class ChoiceEntry<T>(
    string id,
    string labelKey,
    Func<T> get,
    Action<T> set,
    Func<T> defaultValue,
    IReadOnlyList<T> options,
    Func<T, string> optionLabel,
    Func<bool>? isEnabled = null
) : SettingsEntry<T>(id, labelKey, get, set, defaultValue, isEnabled)
{
    public IReadOnlyList<T> Options { get; } = options;

    public string GetOptionLabel(T value) => optionLabel(value);

    /// <inheritdoc/>
    public override SettingsRowState Snapshot(ILocalization localization)
    {
        var labels = new string[Options.Count];
        for (var i = 0; i < Options.Count; i++)
        {
            labels[i] = optionLabel(Options[i]);
        }

        return SettingsRowState.Choice(
            Id,
            localization.GetString(LabelKey) ?? LabelKey,
            CanEdit,
            labels,
            Math.Max(0, IndexOf(Value))
        );
    }

    /// <inheritdoc/>
    public override bool TrySetOptionIndex(int index)
    {
        if (index < 0 || index >= Options.Count)
        {
            return false;
        }

        SetValue(Options[index]);
        return true;
    }

    private int IndexOf(T value)
    {
        for (var i = 0; i < Options.Count; i++)
        {
            if (EqualityComparer<T>.Default.Equals(Options[i], value))
            {
                return i;
            }
        }

        return -1;
    }
}

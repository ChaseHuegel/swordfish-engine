using System;
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
    Func<T, string> optionKey,
    Func<bool>? isEnabled = null
) : SettingsEntry<T>(id, labelKey, get, set, defaultValue, isEnabled)
{
    public IReadOnlyList<T> Options { get; } = options;

    public string GetOptionKey(T value) => optionKey(value);

    /// <inheritdoc/>
    public override SettingsRowState Snapshot()
    {
        var keys = new string[Options.Count];
        for (var i = 0; i < Options.Count; i++)
        {
            keys[i] = optionKey(Options[i]);
        }

        return SettingsRowState.Choice(
            Id,
            LabelKey,
            CanEdit,
            keys,
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

using System;
using Swordfish.Library.Globalization;
using System.Collections.Generic;

namespace WaywardBeyond.Client.UI.Settings;

/// <summary>A setting rendered as a discrete value with previous and next arrows.</summary>
internal sealed class StepperEntry<T>(
    string id,
    string labelKey,
    Func<T> get,
    Action<T> set,
    Func<T> defaultValue,
    IReadOnlyList<T> options,
    Func<T, string> format,
    Func<bool>? isEnabled = null
) : SettingsEntry<T>(id, labelKey, get, set, defaultValue, isEnabled)
{
    public IReadOnlyList<T> Options { get; } = options;

    public string Format(T value) => format(value);

    public int IndexOf(T value)
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

    /// <inheritdoc/>
    public override SettingsRowState Snapshot(ILocalization localization)
    {
        int index = IndexOf(Value);
        return SettingsRowState.Stepper(
            Id,
            localization.GetString(LabelKey) ?? LabelKey,
            CanEdit,
            index >= 0 ? format(Options[index]) : string.Empty,
            CanEdit && index > 0,
            CanEdit && index >= 0 && index < Options.Count - 1
        );
    }

    /// <inheritdoc/>
    public override int Step(int delta)
    {
        int index = IndexOf(Value);
        int next = Math.Clamp(index + delta, 0, Options.Count - 1);
        if (index < 0 || next == index)
        {
            return 0;
        }

        SetValue(Options[next]);
        return next > index ? 1 : -1;
    }
}

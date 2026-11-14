using System;
using System.Collections.Generic;

namespace WaywardBeyond.Client.UI.Settings;

/// <summary>A setting rendered as a discrete value with previous and next arrows.</summary>
internal sealed class StepperEntry<T>(
    string id,
    string labelKey,
    Func<T> get,
    Action<T> set,
    Func<T> defaultValue,
    Func<IReadOnlyList<T>> options,
    Func<T, string> format,
    Func<T, string>? valueKey = null,
    Func<bool>? isEnabled = null
) : SettingsEntry<T>(id, labelKey, get, set, defaultValue, isEnabled)
{
    public string Format(T value) => format(value);

    /// <inheritdoc/>
    public override SettingsRowState Snapshot()
    {
        IReadOnlyList<T> values = options();
        int index = IndexOf(values, Value);
        string key = index >= 0 && valueKey != null ? valueKey(values[index]) : string.Empty;
        return SettingsRowState.Stepper(
            Id,
            LabelKey,
            CanEdit,
            key.Length > 0 ? string.Empty : index >= 0 ? format(values[index]) : string.Empty,
            key,
            CanEdit && index > 0,
            CanEdit && index >= 0 && index < values.Count - 1
        );
    }

    /// <inheritdoc/>
    public override int Step(int delta)
    {
        IReadOnlyList<T> values = options();
        int index = IndexOf(values, Value);
        int next = Math.Clamp(index + delta, 0, values.Count - 1);
        if (index < 0 || next == index)
        {
            return 0;
        }

        SetValue(values[next]);
        return next > index ? 1 : -1;
    }

    private static int IndexOf(IReadOnlyList<T> values, T value)
    {
        for (var i = 0; i < values.Count; i++)
        {
            if (EqualityComparer<T>.Default.Equals(values[i], value))
            {
                return i;
            }
        }

        return -1;
    }
}

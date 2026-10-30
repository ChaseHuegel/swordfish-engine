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
    public override void Render(SettingsRenderer renderer)
    {
        renderer.DrawStepper(this);
    }
}

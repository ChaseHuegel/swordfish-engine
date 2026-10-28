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
    Func<T, string> optionLabel,
    Func<bool>? isEnabled = null
) : SettingsEntry<T>(id, labelKey, get, set, defaultValue, isEnabled)
{
    public IReadOnlyList<T> Options { get; } = options;

    public string GetOptionLabel(T value) => optionLabel(value);

    /// <inheritdoc/>
    public override void Render(SettingsRenderer renderer)
    {
        renderer.DrawChoice(this);
    }
}

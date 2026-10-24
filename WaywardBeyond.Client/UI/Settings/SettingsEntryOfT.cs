using System;
using System.Collections.Generic;

namespace WaywardBeyond.Client.UI.Settings;

/// <summary>A settings row that reads and writes one typed draft value.</summary>
internal abstract class SettingsEntry<T>(
    string id,
    string labelKey,
    Func<T> get,
    Action<T> set,
    Func<T> defaultValue,
    Func<bool>? isEnabled
) : SettingsEntry(id, labelKey, isEnabled)
{
    private readonly Func<T> _get = get;
    private readonly Action<T> _set = set;
    private readonly Func<T> _defaultValue = defaultValue;
    private T _baseline = get();

    /// <summary>Gets the current draft value.</summary>
    public T Value => _get();

    /// <summary>Sets the current draft value.</summary>
    public void SetValue(T value)
    {
        _set(value);
    }

    /// <inheritdoc/>
    public override bool IsDirty => !EqualityComparer<T>.Default.Equals(_get(), _baseline);

    /// <inheritdoc/>
    public override void CaptureBaseline()
    {
        _baseline = _get();
    }

    /// <inheritdoc/>
    public override void ResetToDefault()
    {
        _set(_defaultValue());
    }
}

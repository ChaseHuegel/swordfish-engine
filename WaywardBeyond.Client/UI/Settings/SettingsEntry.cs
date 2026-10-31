using System;

namespace WaywardBeyond.Client.UI.Settings;

/// <summary>A settings row with a value and a control.</summary>
internal abstract class SettingsEntry : SettingsRow
{
    private readonly Func<bool>? _isEnabled;

    protected SettingsEntry(string id, string labelKey, Func<bool>? isEnabled)
    {
        Id = id;
        LabelKey = labelKey;
        _isEnabled = isEnabled;
    }

    public string Id { get; }

    public string LabelKey { get; }

    public bool CanEdit => _isEnabled?.Invoke() ?? true;

    /// <summary>Gets whether the value differs from its baseline.</summary>
    public abstract bool IsDirty { get; }

    /// <summary>Selects an option by index for choice-like rows.</summary>
    public virtual bool TrySetOptionIndex(int index) => false;

    /// <summary>Steps a stepper row and returns the applied direction.</summary>
    public virtual int Step(int delta) => 0;

    /// <summary>Sets the value of a slider row.</summary>
    public virtual bool TrySetSlider(float value) => false;

    /// <summary>Marks the current value as the baseline.</summary>
    public abstract void CaptureBaseline();

    /// <summary>Restores the setting's default value.</summary>
    public abstract void ResetToDefault();
}

using System;
using WaywardBeyond.Client.UI.Theme;

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

    /// <inheritdoc/>
    public override int Height => UiMetrics.ROW_HEIGHT;

    /// <summary>Gets whether the value differs from its baseline.</summary>
    public abstract bool IsDirty { get; }

    /// <summary>Marks the current value as the baseline.</summary>
    public abstract void CaptureBaseline();

    /// <summary>Restores the setting's default value.</summary>
    public abstract void ResetToDefault();
}

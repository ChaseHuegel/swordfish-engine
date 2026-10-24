using System;

namespace WaywardBeyond.Client.UI.Settings;

/// <summary>A numeric setting rendered as a draggable slider.</summary>
internal sealed class SliderEntry(
    string id,
    string labelKey,
    Func<float> get,
    Action<float> set,
    Func<float> defaultValue,
    float min,
    float max,
    float step,
    Func<float, string> format,
    Func<bool>? isEnabled = null
) : SettingsEntry<float>(id, labelKey, get, set, defaultValue, isEnabled)
{
    public float Min { get; } = min;

    public float Max { get; } = max;

    public float Step { get; } = step;

    public string Format(float value) => format(value);

    /// <inheritdoc/>
    public override void Render(SettingsRenderer renderer, int y)
    {
        renderer.DrawSlider(this, y);
    }
}

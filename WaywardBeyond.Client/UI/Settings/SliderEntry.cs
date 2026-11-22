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

    public float StepSize { get; } = step;

    public string Format(float value) => format(value);

    /// <inheritdoc/>
    public override SettingsRowState Snapshot()
    {
        return SettingsRowState.Slider(
            Id,
            LabelKey,
            CanEdit,
            Value,
            Min,
            Max,
            StepSize,
            format(Value)
        );
    }

    /// <inheritdoc/>
    public override int Step(int delta)
    {
        if (!CanEdit || StepSize <= 0f || delta == 0)
        {
            return 0;
        }

        return SetValueClamped(Value + delta * StepSize);
    }

    /// <inheritdoc/>
    public override int TrySetSliderRatio(float ratio)
    {
        float value = Min + Math.Clamp(ratio, 0f, 1f) * (Max - Min);
        if (StepSize > 0f)
        {
            value = MathF.Round((value - Min) / StepSize) * StepSize + Min;
        }

        return SetValueClamped(value);
    }

    private int SetValueClamped(float value)
    {
        float previous = Value;
        SetValue(Math.Clamp(value, Min, Max));
        float applied = Value;

        if (applied == previous)
        {
            return 0;
        }

        return applied > previous ? 1 : -1;
    }
}

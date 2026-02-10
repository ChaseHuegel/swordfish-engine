using System;
using System.Globalization;
using System.Numerics;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Audio;
using Swordfish.Graphics;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Configuration;
using WaywardBeyond.Client.Core.Numerics;

namespace WaywardBeyond.Client.Core.UI;

internal static partial class Widgets
{
    private const string INCREASE_UNICODE = "\uf0fe";
    private const string DECREASE_UNICODE = "\uf146";
    
    /// <summary>
    ///     Creates a text-only number control with increment and decrement buttons.
    /// </summary>
    /// <returns>The new value after any changes.</returns>
    public static int NumberControl(this UIBuilder<Material> ui, string id, string text, int value, Int2 constraints, Int2 display, int steps, IAudioService audioService, VolumeSettings volumeSettings, Action<int>? onValueChanged = null)
    {
        int stepAmount = constraints.Length / steps;
        
        var valueToDisplay = (int)Math.Round(MathS.RangeToRange(value, constraints.Min, constraints.Max, display.Min, display.Max));
        ChangeType changeType = NumberControl(ui, id, text, valueToDisplay.ToString(CultureInfo.CurrentCulture));
        switch (changeType)
        {
            case ChangeType.None:
                break;
            case ChangeType.Increase:
                value += stepAmount;
                break;
            case ChangeType.Decrease:
                value -= stepAmount;
                break;
        }

        value = Math.Clamp(value, constraints.Min, constraints.Max);
        onValueChanged?.Invoke(value);

        if (changeType != ChangeType.None)
        {
            Interactions.Click.WithButtonDecreaseAudio(audioService, volumeSettings);
        }
        
        return value;
    }
    
    /// <summary>
    ///     Creates a text-only number control with increment and decrement buttons.
    /// </summary>
    /// <returns>The new value after any changes.</returns>
    public static float NumberControl(this UIBuilder<Material> ui, string id, string text, float value, Float2 constraints, Int2 display, int steps, IAudioService audioService, VolumeSettings volumeSettings, Action<float>? onValueChanged = null)
    {
        float stepAmount = constraints.Length / steps;
        
        var valueToDisplay = (int)Math.Round(MathS.RangeToRange(value, constraints.Min, constraints.Max, display.Min, display.Max));
        ChangeType changeType = NumberControl(ui, id, text, valueToDisplay.ToString(CultureInfo.CurrentCulture));
        switch (changeType)
        {
            case ChangeType.None:
                break;
            case ChangeType.Increase:
                value += stepAmount;
                break;
            case ChangeType.Decrease:
                value -= stepAmount;
                break;
        }

        value = Math.Clamp(value, constraints.Min, constraints.Max);
        onValueChanged?.Invoke(value);

        if (changeType != ChangeType.None)
        {
            Interactions.Click.WithButtonDecreaseAudio(audioService, volumeSettings);
        }
        
        return value;
    }
    
    private static ChangeType NumberControl(UIBuilder<Material> ui, string id, string text, string value)
    {
        var changeType = ChangeType.None;
        
        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.Horizontal;
            ui.Constraints = new Constraints
            {
                Width = new Relative(1f),
            };
            
            using (ui.Text(text))
            {
                ui.FontSize = 20;
                ui.Color = new Vector4(0.65f, 0.65f, 0.65f, 1f);
            }
            
            using (ui.Element())
            {
                ui.Constraints = new Constraints
                {
                    Width = new Fill(),
                    Height = new Fill(),
                };
            }
                        
            using (ui.Element())
            {
                ui.Spacing = 8;
                using (ui.Element(id + "_Decrease"))
                {
                    bool clicked = ui.Clicked();
                    bool hovering = ui.Hovering();
                    
                    using (ui.Text(DECREASE_UNICODE, fontID: "Font Awesome 6 Free Regular"))
                    {
                        ui.FontSize = 20;
                        
                        if (clicked)
                        {
                            changeType = ChangeType.Decrease;
                            ui.Color = new Vector4(0f, 0f, 0f, 1f);
                        }
                        else if (hovering)
                        {
                            ui.Color = new Vector4(1f, 1f, 1f, 1f);
                        }
                        else
                        {
                            ui.Color = new Vector4(0.65f, 0.65f, 0.65f, 1f);
                        }
                    }
                }
                
                using (ui.Text(value))
                {
                    ui.FontSize = 20;
                }
                
                using (ui.Element(id + "_Increase"))
                {
                    bool clicked = ui.Clicked();
                    bool hovering = ui.Hovering();
                    
                    using (ui.Text(INCREASE_UNICODE, fontID: "Font Awesome 6 Free Regular"))
                    {
                        ui.FontSize = 20;
                        
                        if (clicked)
                        {
                            ui.Color = new Vector4(0f, 0f, 0f, 1f);
                            changeType = ChangeType.Increase;
                        }
                        else if (hovering)
                        {
                            ui.Color = new Vector4(1f, 1f, 1f, 1f);
                        }
                        else
                        {
                            ui.Color = new Vector4(0.65f, 0.65f, 0.65f, 1f);
                        }
                    }
                }
            }
        }

        return changeType;
    }

    private enum ChangeType
    {
        None,
        Increase,
        Decrease,
    }
}
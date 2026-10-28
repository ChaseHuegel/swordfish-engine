using System.Numerics;
using Reef;
using Reef.UI;

namespace Reef.Markup.TestComponents;

/// <summary>A deliberately small public widget library used by markup tests.</summary>
public static class SampleWidgets
{
    /// <summary>Draws a simple checkbox and returns the new value.</summary>
    public static bool Checkbox(this UIBuilder<PixelTexture> ui, string id, string text, bool value)
    {
        using (ui.Element(id))
        {
            ui.Constraints = new Reef.UI.Constraints
            {
                Width = new Reef.Constraints.Fixed(180),
                Height = new Reef.Constraints.Fixed(24),
            };
            ui.BackgroundColor = value ? new Vector4(0.2f, 0.6f, 0.3f, 1f) : new Vector4(0.2f, 0.2f, 0.2f, 1f);
            if (ui.Clicked())
            {
                value = !value;
            }

            using (ui.Text(text))
            {
                ui.Padding = new Padding(24, 4, 4, 4);
            }
        }

        return value;
    }

    /// <summary>Draws a two-cell toggle and returns a change result.</summary>
    public static ControlResult<bool> Toggle(
        this UIBuilder<PixelTexture> ui,
        string id,
        string onLabel,
        string offLabel,
        bool value)
    {
        bool changed = false;
        using (ui.Element(id))
        {
            ui.Constraints = new Reef.UI.Constraints
            {
                Width = new Reef.Constraints.Fixed(140),
                Height = new Reef.Constraints.Fixed(24),
            };
            if (ui.Clicked())
            {
                value = !value;
                changed = true;
            }

            using (ui.Text(value ? onLabel : offLabel)) { }
        }

        return new ControlResult<bool>(changed, value);
    }

    /// <summary>Draws a panel using the direct style and constraint parameters.</summary>
    public static void Panel(this UIBuilder<PixelTexture> ui, Style style, Reef.UI.Constraints constraints)
    {
        using (ui.Element())
        {
            ui.Color = style.BackgroundColor;
            ui.Border = style.Border;
            ui.CornerRadius = style.CornerRadius;
            ui.Constraints = constraints;
        }
    }

    /// <summary>Draws a label through the wrapper fallback path.</summary>
    public static void WrappedLabel(this UIBuilder<PixelTexture> ui, string text, int fontSize)
    {
        using (ui.Text(text))
        {
            ui.FontOptions = ui.FontOptions with { Size = fontSize };
        }
    }

    /// <summary>Opens a disposable element so markup children can be emitted inside it.</summary>
    public static UIBuilder<PixelTexture>.Scope Section(this UIBuilder<PixelTexture> ui, string id)
    {
        return ui.Element(id);
    }

    /// <summary>Mutates a ref state value when clicked.</summary>
    public static void RefTextBox(
        this UIBuilder<PixelTexture> ui,
        string id,
        ref TextBoxState state,
        FontOptions fontOptions)
    {
        using (ui.Element(id))
        {
            ui.Constraints = new Reef.UI.Constraints
            {
                Width = new Reef.Constraints.Fixed(180),
                Height = new Reef.Constraints.Fixed(24),
            };
            if (ui.Clicked())
            {
                state.Clicks++;
            }

            using (ui.Text(state.Text))
            {
                ui.FontOptions = fontOptions;
            }
        }
    }
}

/// <summary>A minimal change result used by the toggle test widget.</summary>
public readonly record struct ControlResult<T>(bool Changed, T Value);

/// <summary>A mutable text box state used by the ref binding test widget.</summary>
public sealed class TextBoxState
{
    public string Text { get; set; } = string.Empty;
    public int Clicks { get; set; }
}

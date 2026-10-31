using System.Numerics;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;

namespace WaywardBeyond.Client.UI;

internal static partial class Widgets
{
    private static readonly Vector4 BUTTON_BACKGROUND = FromRgb(18, 24, 38);
    private static readonly Vector4 BUTTON_HOVER = FromRgb(27, 37, 56);
    private static readonly Vector4 BUTTON_PRESSED = FromRgb(22, 40, 56);
    private static readonly Vector4 BUTTON_BORDER = FromRgb(43, 56, 78);
    private static readonly Vector4 BUTTON_BORDER_HOVER = FromRgb(73, 93, 122);
    private static readonly Vector4 BUTTON_TEXT = FromRgb(240, 243, 246);
    private static readonly Vector4 BUTTON_PRESSED_TEXT = FromRgb(165, 243, 252);
    private static readonly Vector4 BUTTON_DISABLED_BACKGROUND = FromRgb(13, 18, 28, 240);
    private static readonly Vector4 BUTTON_DISABLED_BORDER = FromRgb(26, 35, 51);
    private static readonly Vector4 BUTTON_DISABLED_TEXT = FromRgb(69, 84, 107);

    /// <summary>Creates an action button and returns true when clicked.</summary>
    public static bool ActionButton(
        this UIBuilder<Material> ui,
        string id,
        string text,
        ActionButtonOptions options
    )
    {
        bool clicked;

        if (options.Enabled)
        {
            using (ui.Element(id))
            {
                clicked = ui.Clicked();
                bool hovering = ui.Hovering();
                DrawActionButton(ui, text, options, enabled: true, hovering, clicked);
            }
        }
        else
        {
            using (ui.Element())
            {
                DrawActionButton(ui, text, options, enabled: false, hovering: false, clicked: false);
            }

            clicked = false;
        }

        return clicked;
    }

    private static void DrawActionButton(
        UIBuilder<Material> ui,
        string text,
        ActionButtonOptions options,
        bool enabled,
        bool hovering,
        bool clicked
    )
    {
        Vector4 fill;
        Vector4 border;
        Vector4 textColor;

        if (!enabled)
        {
            fill = BUTTON_DISABLED_BACKGROUND;
            border = BUTTON_DISABLED_BORDER;
            textColor = BUTTON_DISABLED_TEXT;
        }
        else if (clicked)
        {
            fill = BUTTON_PRESSED;
            border = BUTTON_BORDER_HOVER;
            textColor = BUTTON_PRESSED_TEXT;
        }
        else if (hovering)
        {
            fill = BUTTON_HOVER;
            border = BUTTON_BORDER_HOVER;
            textColor = BUTTON_TEXT;
        }
        else
        {
            fill = BUTTON_BACKGROUND;
            border = BUTTON_BORDER;
            textColor = BUTTON_TEXT;
        }

        ui.LayoutDirection = LayoutDirection.None;
        ui.Color = fill;
        ui.Border = new BorderStyle
        {
            Thickness = 1,
            Color = border,
        };
        ui.Padding = new Padding(0);

        if (options.Layout is { } layout)
        {
            if (options.Anchors != default)
            {
                layout.Anchors = options.Anchors;
            }

            ui.Constraints = layout;
        }
        else
        {
            ui.Constraints = new Constraints
            {
                Anchors = options.Anchors,
            };
        }

        ui.ClipConstraints = ClipToBounds();

        using (ui.Text(text))
        {
            ui.FontOptions = new FontOptions
            {
                ID = "Salmon Mono 9 Regular",
                Size = 18,
                NoWrap = true,
            };
            ui.Color = textColor;
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
            };
        }
    }

    /// <summary>Options for an action button.</summary>
    public readonly record struct ActionButtonOptions(
        bool Enabled = true,
        Constraints? Layout = null,
        Anchors Anchors = default
    );
}

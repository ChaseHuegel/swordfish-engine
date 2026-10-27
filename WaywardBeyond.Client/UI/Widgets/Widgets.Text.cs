using System.Numerics;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using WaywardBeyond.Client.UI.Theme;

namespace WaywardBeyond.Client.UI;

internal static partial class Widgets
{
    /// <summary>Draws a single-line label inside a fixed rectangle.</summary>
    public static void Label(
        this UIBuilder<Material> ui,
        string text,
        IntRect rect,
        int fontSize,
        Vector4 color,
        Anchors anchors = Anchors.Left | Anchors.Center,
        string? fontId = null
    )
    {
        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.None;
            ui.Color = UiTheme.Transparent;
            ui.Constraints = RectConstraints(rect);
            ui.ClipConstraints = RectConstraints(rect);

            using (ui.Text(text))
            {
                var fontOptions = new FontOptions
                {
                    ID = fontId ?? UiTheme.FONT_UI,
                    Size = fontSize,
                    NoWrap = true,
                };
                ui.FontOptions = fontOptions;
                ui.Color = color;
                var constraints = new Constraints
                {
                    Anchors = anchors,
                };

                int iconOffset = IconVerticalOffset(fontOptions);
                if (iconOffset != 0 && (anchors & Anchors.Center) == Anchors.Center)
                {
                    constraints.Y = new Fixed(-iconOffset);
                }

                ui.Constraints = constraints;
            }
        }
    }

    /// <summary>Draws wrapped text inside a fixed rectangle.</summary>
    public static void Paragraph(
        this UIBuilder<Material> ui,
        string text,
        IntRect rect,
        int fontSize,
        Vector4 color,
        Anchors anchors = Anchors.Left | Anchors.Top
    )
    {
        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.None;
            ui.Color = UiTheme.Transparent;
            ui.Constraints = RectConstraints(rect);
            ui.ClipConstraints = RectConstraints(rect);

            using (ui.Text(text))
            {
                ui.FontOptions = new FontOptions
                {
                    ID = UiTheme.FONT_UI,
                    Size = fontSize,
                };
                ui.Color = color;
                ui.Constraints = new Constraints
                {
                    Anchors = anchors,
                };
            }
        }
    }

    /// <summary>Draws a one-pixel divider inside a fixed rectangle.</summary>
    public static void Divider(this UIBuilder<Material> ui, IntRect rect, Vector4 color)
    {
        using (ui.Element())
        {
            ui.Color = color;
            ui.Constraints = RectConstraints(rect);
        }
    }
}

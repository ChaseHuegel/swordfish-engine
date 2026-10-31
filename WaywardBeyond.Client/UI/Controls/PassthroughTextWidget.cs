using Reef;
using Reef.UI;
using Swordfish.Graphics;

namespace WaywardBeyond.Client.UI.Controls;

/// <summary>Draws measured, non-interactive rich text that hit-testing ignores.</summary>
internal static class PassthroughTextWidget
{
    /// <summary>Draws text under an identifier so later frames can measure it.</summary>
    public static void PassthroughText(
        this UIBuilder<Material> ui,
        string id,
        string text,
        Style style,
        FontOptions fontOptions
    )
    {
        fontOptions.ID ??= ui.DefaultFontID;

        using (ui.Text(text))
        {
            ui.ID = id;
            ui.Passthrough = true;
            ui.FontOptions = fontOptions;
            ui.Color = style.Color;
        }
    }
}

using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using WaywardBeyond.Client.UI.Theme;

namespace WaywardBeyond.Client.UI;

internal static partial class Widgets
{
    /// <summary>Draws a light horizontal dotted divider inside a fixed rectangle.</summary>
    public static void DottedDivider(this UIBuilder<Material> ui, IntRect rect)
    {
        const int dotWidth = 2;
        const int dotGap = 4;

        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.None;
            ui.Constraints = RectConstraints(rect);

            for (var offset = 0; offset < rect.Size.X - dotWidth; offset += dotWidth + dotGap)
            {
                using (ui.Element())
                {
                    ui.Color = UiTheme.Border;
                    ui.Constraints = new Constraints
                    {
                        X = new Fixed(offset),
                        Width = new Fixed(dotWidth),
                        Height = new Fixed(rect.Size.Y),
                    };
                }
            }
        }
    }
}

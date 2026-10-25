using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using WaywardBeyond.Client.UI.Theme;

namespace WaywardBeyond.Client.UI;

internal static partial class Widgets
{
    /// <summary>Draws the cyan menu chevron.</summary>
    public static void MenuChevron(this UIBuilder<Material> ui, int x, int y)
    {
        for (var i = 0; i < 3; i++)
        {
            using (ui.Element())
            {
                ui.Color = UiTheme.AccentBright;
                ui.Constraints = new Constraints
                {
                    X = new Fixed(x + i * 2),
                    Y = new Fixed(y + i),
                    Width = new Fixed(2),
                    Height = new Fixed(6 - i * 2),
                };
            }
        }
    }
}

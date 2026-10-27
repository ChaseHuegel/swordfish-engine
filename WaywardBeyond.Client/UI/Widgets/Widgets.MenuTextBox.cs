using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.IO;
using WaywardBeyond.Client.UI.Theme;

namespace WaywardBeyond.Client.UI;

internal static partial class Widgets
{
    /// <summary>Draws a clipped menu text box within a fixed rectangle and returns its interactions.</summary>
    public static Interactions MenuTextBox(
        this UIBuilder<Material> ui,
        string id,
        ref TextBoxState state,
        IntRect rect,
        int fontSize,
        IInputService inputService
    )
    {
        using (ui.Element())
        {
            ui.Constraints = RectConstraints(rect);
            ui.ClipConstraints = new Constraints
            {
                Width = new Fixed(rect.Size.X),
                Height = new Fixed(rect.Size.Y),
            };
            ui.TextBox(
                id,
                ref state,
                new FontOptions { Size = fontSize },
                inputService,
                out Interactions interactions
            );

            return interactions;
        }
    }
}

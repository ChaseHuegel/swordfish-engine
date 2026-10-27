using System;
using System.Numerics;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using WaywardBeyond.Client.UI.Theme;

namespace WaywardBeyond.Client.UI;

internal static partial class Widgets
{
    /// <summary>Draws the modal scrim and a centered panel, then runs the body inside that panel.</summary>
    public static void ModalFrame(
        this UIBuilder<Material> ui,
        int width,
        int height,
        Action<UIBuilder<Material>> body
    )
    {
        ui.ModalFrame(width, height, UiTheme.Scrim, body);
    }

    /// <summary>Draws a modal scrim of the given color and a centered panel, then runs the body inside that panel.</summary>
    public static void ModalFrame(
        this UIBuilder<Material> ui,
        int width,
        int height,
        Vector4 scrim,
        Action<UIBuilder<Material>> body
    )
    {
        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.None;
            ui.Color = scrim;
            ui.Constraints = new Constraints
            {
                Width = new Fill(),
                Height = new Fill(),
            };

            using (ui.Element())
            {
                ui.LayoutDirection = LayoutDirection.None;
                ui.Constraints = new Constraints
                {
                    Anchors = Anchors.Center,
                    Width = new Fixed(width),
                    Height = new Fixed(height),
                };
                ui.ClipConstraints = new Constraints
                {
                    Width = new Fixed(width),
                    Height = new Fixed(height),
                };

                using (ui.ModalPanel(Rect(0, 0, width, height)))
                {
                    body(ui);
                }
            }
        }
    }
}

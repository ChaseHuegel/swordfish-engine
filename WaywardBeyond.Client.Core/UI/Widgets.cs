using System.Numerics;
using Reef;
using Reef.UI;

namespace WaywardBeyond.Client.Core.UI;

internal static class Widgets
{
    public static bool TextButton<T>(this UIBuilder<T> ui, string id, string text, FontOptions fontOptions)
    {
        using (ui.Element(id))
        {
            bool clicked = ui.Clicked();
            bool hovering = ui.Hovering();
            using (ui.Text(text))
            {
                ui.FontOptions = fontOptions;

                if (clicked)
                {
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

            return clicked;
        }
    }
}
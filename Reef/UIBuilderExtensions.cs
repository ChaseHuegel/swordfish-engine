using System.Numerics;
using Reef.UI;

namespace Reef;

public static class UIBuilderExtensions
{
    public static bool Button<TTextureData>(this UIBuilder<TTextureData> ui, string text)
    {
        return Button(ui, id: text, text: text);
    }
    
    public static bool Button<TTextureData>(this UIBuilder<TTextureData> ui, string id, string text)
    {
        using (ui.Element(id))
        {
            bool clicked = ui.Clicked();
            ui.Color = clicked ? new Vector4(0.5f, 0.5f, 0.5f, 1f) : new Vector4(0f, 0f, 0f, 1f);
            ui.Padding = new Padding(left: 4, top: 4, right: 4, bottom: 4);
            using (ui.Text(text)) {}
            return clicked;
        }
    }
}
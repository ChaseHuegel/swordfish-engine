using System.Drawing;
using ImGuiNET;
using Swordfish.Util;

namespace Swordfish.UI.Elements;

public class ColorBlockElement : ContentElement, IColorProperty
{
    public Color Color { get; set; }

    public ColorBlockElement(Color color)
    {
        Color = color;
    }

    protected override void OnRender()
    {
        ImGui.PushStyleColor(ImGuiCol.Text, Color.ToVector4());

        base.OnRender();

        ImGui.PopStyleColor();
    }
}

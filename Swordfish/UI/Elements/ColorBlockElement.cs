using System.Drawing;
using ImGuiNET;
using Swordfish.Library.Extensions;

namespace Swordfish.UI.Elements;

public class ColorBlockElement : ContentElement, IColorProperty
{
    private static readonly Stack<Color> ColorStack = new();

    public Color Color { get; set; }

    public ColorBlockElement(Color color)
    {
        Color = color;
    }

    protected override void OnRender()
    {
        ImGui.PushStyleColor(ImGuiCol.Text, Color.ToVector4());
        ColorStack.Push(Color);

        base.OnRender();

        ColorStack.Pop();
        ImGui.PopStyleColor();
    }

    public static bool TryGetColorBlock(out Color color)
    {
        lock (ColorStack)
            return ColorStack.TryPeek(out color);
    }
}

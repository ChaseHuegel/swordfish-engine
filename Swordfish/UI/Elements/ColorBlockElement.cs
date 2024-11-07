using System.Drawing;
using ImGuiNET;
using Swordfish.Library.Extensions;

namespace Swordfish.UI.Elements;

public class ColorBlockElement(in Color color) : ContentElement, IColorProperty
{
    private static readonly Stack<Color> _colorStack = new();

    public Color Color { get; set; } = color;

    protected override void OnRender()
    {
        ImGui.PushStyleColor(ImGuiCol.Text, Color.ToVector4());
        lock (_colorStack)
        {
            _colorStack.Push(Color);
        }

        base.OnRender();

        lock (_colorStack)
        {
            _colorStack.Pop();
        }
        ImGui.PopStyleColor();
    }

    public static bool TryGetColorBlock(out Color color)
    {
        lock (_colorStack)
        {
            return _colorStack.TryPeek(out color);
        }
    }
}

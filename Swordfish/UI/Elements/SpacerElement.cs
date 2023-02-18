using System.Numerics;
using ImGuiNET;
using Swordfish.Library.Constraints;

namespace Swordfish.UI.Elements;

public class SpacerElement : Element
{
    public IConstraint? Width { get; set; }

    public IConstraint? Height { get; set; }

    public SpacerElement() { }

    public SpacerElement(IConstraint? height)
    {
        Height = height;
    }


    public SpacerElement(IConstraint? width, IConstraint? height)
    {
        Width = width;
        Height = height;
    }

    public SpacerElement(float height)
    {
        Height = new AbsoluteConstraint(height);
    }

    public SpacerElement(float width, float height)
    {
        Width = new AbsoluteConstraint(width);
        Height = new AbsoluteConstraint(height);
    }

    protected override void OnRender()
    {
        Vector2 max = (Parent as IConstraintsProperty)?.Constraints.Max ?? ImGui.GetContentRegionAvail();

        ImGui.Dummy(new Vector2(
            Width?.GetValue(max.X) ?? 0f,
            Height?.GetValue(max.Y) ?? ImGui.GetStyle().ItemSpacing.Y
        ));
    }
}

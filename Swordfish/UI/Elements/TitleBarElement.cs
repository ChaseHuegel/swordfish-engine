using System.Numerics;
using ImGuiNET;
using Swordfish.Library.Constraints;
using Swordfish.UI.ImGuiNET;

namespace Swordfish.UI.Elements;

public class TitleBarElement(in string text) : TextElement(text)
{
    public bool Border { get; set; }

    // ReSharper disable once UnusedMember.Global
    public TitleBarElement(string text, ConstraintAnchor anchor) : this(text, false, anchor)
    {
    }

    public TitleBarElement(string text, bool border) : this(text)
    {
        Border = border;
    }

    public TitleBarElement(string text, bool border, ConstraintAnchor anchor) : this(text, border)
    {
        Constraints.Anchor = anchor;
    }

    protected override void OnRender()
    {
        var spacing = new Vector2(0f, ImGui.GetStyle().FramePadding.Y);

        ImGuiEx.BeginPane(null, new Vector2(-1, 0), false);
        ImGui.Dummy(spacing);

        base.OnRender();

        ImGui.Dummy(spacing);
        ImGuiEx.EndPane();
    }
}

using System.Drawing;
using System.Numerics;
using ImGuiNET;
using Swordfish.Library.Types.Constraints;
using Swordfish.Types.Constraints;
using Swordfish.Util;

namespace Swordfish.UI.Elements;

public class TextElement : Element, ITextProperty, ILabelProperty, IColorProperty, ITooltipProperty, IConstraintsProperty
{
    public string? Text { get; set; }

    public string? Label { get; set; }

    public Color Color { get; set; }

    public bool Wrap { get; set; } = true;

    public Tooltip Tooltip { get; set; }

    public RectConstraints Constraints { get; set; } = new RectConstraints();

    protected ITooltipProperty TooltipProperty => this;
    protected IColorProperty ColorProperty => this;

    public TextElement(string text)
    {
        Text = text;
        Color = Color.White;
    }

    protected override void OnRender()
    {
        Constraints.Max = (Parent as IConstraintsProperty)?.Constraints.Max ?? ImGui.GetContentRegionAvail();
        Vector2 origin = Alignment == ElementAlignment.NONE ? Vector2.Zero : ImGui.GetCursorPos();
        Vector2 position = Constraints.GetPosition();

        switch (Constraints.Anchor)
        {
            case ConstraintAnchor.TOP_CENTER:
                origin -= new Vector2(ImGui.CalcTextSize(Text).X / 2f, 0f);
                break;
            case ConstraintAnchor.TOP_RIGHT:
                origin -= new Vector2(ImGui.CalcTextSize(Text).X, 0f);
                break;
            case ConstraintAnchor.CENTER_LEFT:
                origin -= new Vector2(0f, ImGui.CalcTextSize(Text).Y / 2f);
                break;
            case ConstraintAnchor.CENTER:
                origin -= ImGui.CalcTextSize(Text) / 2f;
                break;
            case ConstraintAnchor.CENTER_RIGHT:
                origin -= new Vector2(ImGui.CalcTextSize(Text).X, ImGui.CalcTextSize(Text).Y / 2f);
                break;
            case ConstraintAnchor.BOTTOM_LEFT:
                origin += new Vector2(0f, ImGui.CalcTextSize(Text).Y);
                break;
            case ConstraintAnchor.BOTTOM_CENTER:
                origin += new Vector2(-ImGui.CalcTextSize(Text).X / 2f, ImGui.CalcTextSize(Text).Y);
                break;
            case ConstraintAnchor.BOTTOM_RIGHT:
                origin += new Vector2(-ImGui.CalcTextSize(Text).X, ImGui.CalcTextSize(Text).Y);
                break;
        }

        ImGui.SetCursorPos(origin + position);

        ImGui.PushStyleColor(ImGuiCol.Text, ColorProperty.GetCurrentColor().ToVector4());

        float labelWidth = ImGui.CalcTextSize(Label).X;
        float labelOffset = Constraints.Width?.GetValue(Constraints.Max.X) ?? Constraints.Max.X;
        if (Wrap) ImGui.PushTextWrapPos(labelOffset - labelWidth);

        if (Text != null && Text.StartsWith('-'))
            ImGui.BulletText(Text.TrimStart('-', ' '));
        else
            ImGui.TextUnformatted(Text);

        if (Wrap) ImGui.PopTextWrapPos();

        TooltipProperty.RenderTooltip();

        if (!string.IsNullOrWhiteSpace(Label))
        {
            ImGui.SameLine();
            ImGui.Dummy(new Vector2(labelOffset - labelWidth - ImGui.GetCursorPos().X + ImGui.GetStyle().FramePadding.X, 0f));
            ImGui.SameLine();
            ImGui.TextUnformatted(Label);
        }

        ImGui.PopStyleColor();
    }
}

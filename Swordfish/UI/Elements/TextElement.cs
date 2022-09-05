using System.Drawing;
using System.Numerics;
using ImGuiNET;
using Swordfish.Library.Types.Constraints;
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

    private ITooltipProperty TooltipProperty => this;

    public TextElement(string text)
    {
        Text = text;
        Color = Color.White;
    }

    protected override void OnRender()
    {
        Constraints.Max = (Parent as IConstraintsProperty)?.Constraints.Max ?? ImGui.GetContentRegionAvail();
        Vector2 origin = Alignment == ElementAlignment.NONE ? Vector2.Zero : ImGui.GetCursorPos();

        switch (Constraints.Anchor)
        {
            case Types.Constraints.ConstraintAnchor.TOP_CENTER:
                origin -= new Vector2(ImGui.CalcTextSize(Text).X / 2f, 0f);
                break;
            case Types.Constraints.ConstraintAnchor.TOP_RIGHT:
                origin -= new Vector2(ImGui.CalcTextSize(Text).X, 0f);
                break;
            case Types.Constraints.ConstraintAnchor.CENTER_LEFT:
                origin -= new Vector2(0f, ImGui.CalcTextSize(Text).Y / 2f);
                break;
            case Types.Constraints.ConstraintAnchor.CENTER:
                origin -= ImGui.CalcTextSize(Text) / 2f;
                break;
            case Types.Constraints.ConstraintAnchor.CENTER_RIGHT:
                origin -= new Vector2(ImGui.CalcTextSize(Text).X, ImGui.CalcTextSize(Text).Y / 2f);
                break;
            case Types.Constraints.ConstraintAnchor.BOTTOM_LEFT:
                origin += new Vector2(0f, ImGui.CalcTextSize(Text).Y);
                break;
            case Types.Constraints.ConstraintAnchor.BOTTOM_CENTER:
                origin += new Vector2(-ImGui.CalcTextSize(Text).X / 2f, ImGui.CalcTextSize(Text).Y);
                break;
            case Types.Constraints.ConstraintAnchor.BOTTOM_RIGHT:
                origin += new Vector2(-ImGui.CalcTextSize(Text).X, ImGui.CalcTextSize(Text).Y);
                break;
        }

        ImGui.SetCursorPos(origin + Constraints.GetPosition());

        if (Wrap) ImGui.PushTextWrapPos();
        ImGui.PushStyleColor(ImGuiCol.Text, Color.ToVector4());

        if (string.IsNullOrWhiteSpace(Label))
        {
            if (Text != null && Text.StartsWith('-'))
                ImGui.BulletText(Text.TrimStart('-', ' '));
            else
                ImGui.TextUnformatted(Text);
        }
        else
        {
            ImGui.LabelText(Label, Text);
        }

        ImGui.PopStyleColor();
        if (Wrap) ImGui.PopTextWrapPos();

        TooltipProperty.RenderTooltip();
    }
}

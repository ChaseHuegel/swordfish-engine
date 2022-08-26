using System.Drawing;
using ImGuiNET;
using Swordfish.Util;

namespace Swordfish.UI.Elements;

public class TextElement : Element, ITextProperty, ILabelProperty, IColorProperty, ITooltipProperty
{
    public string? Text { get; set; }

    public string? Label { get; set; }

    public Color Color { get; set; }

    public bool Wrap { get; set; } = true;

    public Tooltip Tooltip { get; set; }

    private ITooltipProperty TooltipProperty => this;

    public TextElement(string text)
    {
        Text = text;
        Color = Color.White;
    }

    protected override void OnRender()
    {
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

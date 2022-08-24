using ImGuiNET;

namespace Swordfish.UI.Elements;

public class TextElement : Element, ITextElement
{
    public string? Text { get; set; }

    public TextElement(string text)
    {
        Text = text;
    }

    protected override void OnRender()
    {
        ImGui.Text(Text);
    }
}

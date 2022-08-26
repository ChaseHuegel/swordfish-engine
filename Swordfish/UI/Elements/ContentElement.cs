using ImGuiNET;
using Swordfish.Library.Types;

namespace Swordfish.UI.Elements;

public abstract class ContentElement : Element, IContentElement
{
    public ContentSeparator ContentSeparator { get; set; }

    public LockedList<IElement> Content { get; } = new();

    protected override void OnRender()
    {
        Content.ForEach((element) =>
        {
            element.Render();
            RenderContentSeparator();
        });
    }

    protected virtual void RenderContentSeparator()
    {
        switch (ContentSeparator)
        {
            case ContentSeparator.DIVIDER:
                ImGui.Separator();
                break;
            case ContentSeparator.SPACER:
                ImGui.Spacing();
                break;
            default:
                break;
        }
    }
}

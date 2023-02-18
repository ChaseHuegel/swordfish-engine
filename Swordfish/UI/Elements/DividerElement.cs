using ImGuiNET;

namespace Swordfish.UI.Elements;

public class DividerElement : Element
{
    protected override void OnRender()
    {
        ImGui.Separator();
    }
}

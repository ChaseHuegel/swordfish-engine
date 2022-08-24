using ImGuiNET;
using Swordfish.Library.Diagnostics;

namespace Swordfish.UI.Elements;

public class WindowElement : ContentElement, INamedElement
{
    public string Name { get; set; }

    public ImGuiWindowFlags Flags { get; set; }

    public bool Open { get; set; }

    public WindowElement(string name)
    {
        Name = name;
    }

    protected override void OnRender()
    {
        ImGui.Begin($"{Name}##{Uid}", Flags);
        Open = ImGui.IsWindowCollapsed();

        base.OnRender();

        ImGui.End();
    }
}

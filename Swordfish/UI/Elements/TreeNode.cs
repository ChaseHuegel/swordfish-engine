using ImGuiNET;

namespace Swordfish.UI.Elements;

public class TreeNode : ContentElement, IUniqueNameProperty
{
    public string Name { get; set; }

    public string UniqueName => uniqueName ??= Name + "##" + Uid;
    private string? uniqueName;

    public TreeNode(string name)
    {
        Name = name;
    }

    protected override void OnRender()
    {
        if (ImGui.TreeNodeEx(UniqueName, Content.Count > 0 ? ImGuiTreeNodeFlags.None : ImGuiTreeNodeFlags.Leaf, Name))
        {
            base.OnRender();
            ImGui.TreePop();
        }
    }
}

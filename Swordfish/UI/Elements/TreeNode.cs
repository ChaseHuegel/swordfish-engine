using ImGuiNET;

namespace Swordfish.UI.Elements;

public class TreeNode : ContentElement, IUniqueNameProperty
{
    public static TreeNode? Selected { get; private set; }

    public string Name { get; set; }

    public bool Selectable { get; set; } = true;

    public string UniqueName => uniqueName ??= Name + "##" + Uid;
    private string? uniqueName;

    public TreeNode(string name)
    {
        Name = name;
    }

    protected override void OnRender()
    {
        bool opened = ImGui.TreeNodeEx(UniqueName, Content.Count > 0 ? ImGuiTreeNodeFlags.None : ImGuiTreeNodeFlags.Leaf | (Selected == this ? ImGuiTreeNodeFlags.Selected : 0), Name);

        if (Selectable && ImGui.IsItemClicked())
            Selected = this;

        if (opened)
        {
            base.OnRender();
            ImGui.TreePop();
        }
    }
}

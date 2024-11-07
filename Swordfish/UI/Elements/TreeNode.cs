using ImGuiNET;
using Swordfish.Library.Types;

namespace Swordfish.UI.Elements;

public class TreeNode(in string? name) : ContentElement, IUniqueNameProperty
{
    public static DataBinding<TreeNode?> Selected { get; } = new();

    public string? Name { get; set; } = name;

    public bool Selectable { get; set; } = true;

    public string UniqueName => _uniqueName ??= Name + "##" + UID;
    private string? _uniqueName;

    protected override void OnRender()
    {
        bool opened = ImGui.TreeNodeEx(UniqueName, Content.Count > 0 ? ImGuiTreeNodeFlags.None : ImGuiTreeNodeFlags.Leaf | (Selected.Get() == this ? ImGuiTreeNodeFlags.Selected : 0), Name);

        if (Selectable && ImGui.IsItemClicked())
        {
            Selected.Set(this);
        }

        if (opened)
        {
            base.OnRender();
            ImGui.TreePop();
        }
    }
}

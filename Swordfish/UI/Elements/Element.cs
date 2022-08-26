using ImGuiNET;
using Swordfish.Library.Types.Constraints;

namespace Swordfish.UI.Elements;

public abstract class Element : IElement, IAlignmentProperty
{
    private static ulong NewUid() => Interlocked.Increment(ref CurrentUid);
    private static ulong CurrentUid;

    public ulong Uid { get; }

    public bool Enabled { get; set; } = true;

    public bool Visible { get; set; } = true;

    public ElementAlignment Alignment { get; set; }

    protected abstract void OnRender();

    public Element()
    {
        Uid = NewUid();
    }

    public void Render()
    {
        if (!Visible)
            return;

        ImGui.BeginDisabled(!Enabled);

        if (Alignment == ElementAlignment.HORIZONTAL)
            ImGui.SameLine();

        OnRender();

        ImGui.EndDisabled();
    }

}

using System.Numerics;
using ImGuiNET;
using Swordfish.Library.Types.Constraints;

namespace Swordfish.UI.Elements;

public abstract class Element : IElement
{
    private static ulong NewUid() => Interlocked.Increment(ref CurrentUid);
    private static ulong CurrentUid;

    public ulong Uid { get; }

    public bool Enabled { get; set; } = true;

    public bool Visible { get; set; } = true;

    public ElementAlignment Alignment { get; set; }

    public IContentElement? Parent { get; set; }

    protected abstract void OnRender();

    public Element()
    {
        Uid = NewUid();
    }

    public void Render()
    {
        if (!Visible)
            return;

        if (Alignment == ElementAlignment.HORIZONTAL)
            ImGui.SameLine();

        ImGui.BeginDisabled(!Enabled);
        OnRender();
        ImGui.EndDisabled();
    }

}

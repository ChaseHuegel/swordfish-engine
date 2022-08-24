using ImGuiNET;

namespace Swordfish.UI.Elements;

public abstract class Element : IElement
{
    private static ulong NewUid() => Interlocked.Increment(ref CurrentUid);
    private static ulong CurrentUid;

    public bool Enabled { get; set; } = true;
    public bool Visible { get; set; } = true;

    protected ulong Uid;

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
        OnRender();
        ImGui.EndDisabled();
    }

}

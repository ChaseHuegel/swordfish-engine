using ImGuiNET;

namespace Swordfish.UI.Elements;

public abstract class Element : IElement
{
    protected static IUIContext UIContext => SwordfishEngine.Kernel.Get<IUIContext>();

    private static ulong NewUid() => Interlocked.Increment(ref CurrentUid);
    private static ulong CurrentUid;

    private volatile bool Focusing;
    private volatile bool Unfocusing;

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

        if (Unfocusing)
        {
            ImGui.SetKeyboardFocusHere(-1);
            Unfocusing = false;
        }

        if (Focusing)
        {
            ImGui.SetWindowFocus();
            ImGui.SetKeyboardFocusHere();
            Focusing = false;
        }

        OnRender();

        ImGui.EndDisabled();
    }

    public void Focus()
    {
        Focusing = true;
    }

    public void Unfocus()
    {
        Unfocusing = true;
    }

}

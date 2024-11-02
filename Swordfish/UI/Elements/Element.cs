using ImGuiNET;

namespace Swordfish.UI.Elements;

public abstract class Element : IElement, IInternalElement
{
    protected IUIContext? UIContext { get; set; }

    private static ulong NewUid() => Interlocked.Increment(ref CurrentUid);
    private static ulong CurrentUid;

    private volatile bool Focusing;
    private volatile bool Unfocusing;

    public ulong UID { get; }

    public bool Enabled { get; set; } = true;

    public bool Visible { get; set; } = true;

    public ElementAlignment Alignment { get; set; }

    public IContentElement? Parent { get; set; }

    protected abstract void OnRender();

    public Element()
    {
        UID = NewUid();
    }

    void IInternalElement.SetUIContext(IUIContext? uiContext)
    {
        UIContext = uiContext;
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

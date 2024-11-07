using ImGuiNET;

namespace Swordfish.UI.Elements;

public abstract class Element : IElement, IInternalElement
{
    protected IUIContext? UIContext { get; set; }

    private static ulong NewUid() => Interlocked.Increment(ref _currentUid);
    private static ulong _currentUid;

    private volatile bool _focus;
    private volatile bool _unfocus;

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
        {
            return;
        }

        if (Alignment == ElementAlignment.HORIZONTAL)
        {
            ImGui.SameLine();
        }

        ImGui.BeginDisabled(!Enabled);

        if (_unfocus)
        {
            ImGui.SetKeyboardFocusHere(-1);
            _unfocus = false;
        }

        if (_focus)
        {
            ImGui.SetWindowFocus();
            ImGui.SetKeyboardFocusHere();
            _focus = false;
        }

        OnRender();

        ImGui.EndDisabled();
    }

    // ReSharper disable once UnusedMember.Global
    public void Focus()
    {
        _focus = true;
    }

    // ReSharper disable once MemberCanBeProtected.Global
    public void Unfocus()
    {
        _unfocus = true;
    }

}

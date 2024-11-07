using System.Drawing;
using System.Numerics;
using ImGuiNET;
using Swordfish.Library.Constraints;
using Swordfish.Library.Extensions;
using System.Runtime.CompilerServices;
using Swordfish.Types;

namespace Swordfish.UI.Elements;

//  TODO very very incomplete MVP, copy-pasted TextElement and left almost unchanged
//  TODO this could likely inherit TextElement
public class InputTextElement : Element, ITextProperty, ILabelProperty, IColorProperty, ITooltipProperty, IConstraintsProperty, IUniqueNameProperty
{
    public string? Name { get; set; }

    public string UniqueName => _uniqueName ??= Name + "##" + UID;
    private string? _uniqueName;

    public EventHandler<string?>? Submit;

    public string? Text {
        get => _text;
        set => _text = value;
    }
    private string? _text;

    public int Length { get; set; }

    public string? Label { get; set; }

    public Color Color { get; set; }

    public bool Wrap { get; set; } = true;

    public Tooltip Tooltip { get; set; }

    public RectConstraints Constraints { get; set; } = new();

    protected ITooltipProperty TooltipProperty => this;
    protected IColorProperty ColorProperty => this;

    public InputTextElement(string text, int length = 32)
    {
        Text = text;
        Length = length;
        Color = Color.White;
        Submit = null;
    }

    protected override void OnRender()
    {
        Constraints.Max = ImGui.GetContentRegionAvail();
        Vector2 origin = Alignment == ElementAlignment.NONE ? Vector2.Zero : ImGui.GetCursorPos();
        Vector2 position = Constraints.GetPosition();

        switch (Constraints.Anchor)
        {
            case ConstraintAnchor.TOP_CENTER:
                origin -= new Vector2(ImGui.CalcTextSize(Text).X / 2f, 0f);
                break;
            case ConstraintAnchor.TOP_RIGHT:
                origin -= new Vector2(ImGui.CalcTextSize(Text).X, 0f);
                break;
            case ConstraintAnchor.CENTER_LEFT:
                origin -= new Vector2(0f, ImGui.CalcTextSize(Text).Y / 2f);
                break;
            case ConstraintAnchor.CENTER:
                origin -= ImGui.CalcTextSize(Text) / 2f;
                break;
            case ConstraintAnchor.CENTER_RIGHT:
                origin -= new Vector2(ImGui.CalcTextSize(Text).X, ImGui.CalcTextSize(Text).Y / 2f);
                break;
            case ConstraintAnchor.BOTTOM_LEFT:
                origin += new Vector2(0f, ImGui.CalcTextSize(Text).Y);
                break;
            case ConstraintAnchor.BOTTOM_CENTER:
                origin += new Vector2(-ImGui.CalcTextSize(Text).X / 2f, ImGui.CalcTextSize(Text).Y);
                break;
            case ConstraintAnchor.BOTTOM_RIGHT:
                origin += new Vector2(-ImGui.CalcTextSize(Text).X, ImGui.CalcTextSize(Text).Y);
                break;
        }

        ImGui.SetCursorPos(origin + position);

        ImGui.PushStyleColor(ImGuiCol.Text, ColorProperty.GetCurrentColor().ToVector4());

        float labelWidth = ImGui.CalcTextSize(Label).X;
        float width = Constraints.Width?.GetValue(Constraints.Max.X) ?? Constraints.Max.X;
        if (Wrap)
        {
            ImGui.PushTextWrapPos(width - labelWidth);
        }

        if (Text != null && Text.StartsWith("- "))
        {
            ImGui.BulletText(Text.TrimStart('-', ' '));
        }
        else if (ImGui.InputText(UniqueName, ref _text, (uint)Length, ImGuiInputTextFlags.EnterReturnsTrue))
        {
            Unfocus();
            UIContext.ThreadContext.Post(InputCallback!, this);
        }

        if (Wrap)
        {
            ImGui.PopTextWrapPos();
        }

        TooltipProperty.RenderTooltip();

        if (!string.IsNullOrWhiteSpace(Label))
        {
            ImGui.SameLine();
            ImGui.Dummy(new Vector2(width - labelWidth - ImGui.GetCursorPos().X + ImGui.GetStyle().FramePadding.X, 0f));
            ImGui.SameLine();
            ImGui.TextUnformatted(Label);
        }

        ImGui.PopStyleColor();
    }

    //  Events should be invoked from the UI's sync context to ensure execution order is
    //  respected and we aren't invoking potentially state-changing code in-line.
    private void InputCallback(object target)
    {
        var inputText = Unsafe.As<InputTextElement>(target);
        inputText!.Submit?.Invoke(inputText, inputText.Text);
    }
}

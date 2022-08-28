using System.Numerics;
using ImGuiNET;

namespace Swordfish.UI.Elements;

public class MenuBarElement : ContentElement, IMenuBarElement
{
    public Tooltip Tooltip { get; set; }

    protected ITooltipProperty TooltipProperty => this;

    protected override void OnRender()
    {
        //  TODO this is hard coded for testing and should be turned into proper elements.
        ImGui.BeginMainMenuBar();

        base.OnRender();

        if (ImGui.BeginMenu("File", true))
            ImGui.EndMenu();

        if (ImGui.BeginMenu("Edit", true))
            ImGui.EndMenu();

        if (ImGui.BeginMenu("View", true))
            ImGui.EndMenu();

        if (ImGui.BeginMenu("Tools", true))
            ImGui.EndMenu();

        if (ImGui.BeginMenu("Run", true))
            ImGui.EndMenu();

        if (ImGui.BeginMenu("Help", true))
            ImGui.EndMenu();

        float padding = ImGui.GetStyle().FramePadding.X + ImGui.GetStyle().WindowPadding.X;
        float textWidth = ImGui.CalcTextSize("Swordfish Engine 2.0.0").X;
        float windowWidth = SwordfishEngine.MainWindow.GetSize().X;
        ImGui.SetCursorPos(new Vector2(windowWidth - textWidth - padding, 0f));
        ImGui.Text("Swordfish Engine 2.0.0");

        ImGui.EndMainMenuBar();
    }
}

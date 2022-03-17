using System.Numerics;

using ImGuiNET;

using OpenTK.Windowing.Desktop;

using Swordfish.Core.Rendering.UI.Elements.Interfaces;
using Swordfish.Library.Diagnostics;

namespace Swordfish.Core.Rendering.UI.Elements.Diagnostics
{
    public class ConsoleWindow : Element
    {
        public override void OnUpdate()
        {
            Enabled = Debug.Console;
        }

        public override void OnShow()
        {
            MonitorInfo display = GLHelper.GetPrimaryDisplay();

            ImGui.SetNextWindowPos(new Vector2(0, Engine.Settings.Window.HEIGHT - Engine.Settings.Window.HEIGHT * 0.2f), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSize( new Vector2(Engine.Settings.Window.WIDTH, Engine.Settings.Window.HEIGHT * 0.2f), ImGuiCond.FirstUseEver);

            ImGui.Begin(Name);
                ImGui.BeginChild("scrollview", Vector2.Zero, false, ImGuiWindowFlags.AlwaysVerticalScrollbar);

                ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
                    foreach (string line in Logger.Writer.GetLines(100))
                        ImGui.TextWrapped(line);
                ImGui.PopStyleVar();

                //  Auto scroll if the bar is at the bottom
                if (ImGui.GetScrollY() >= ImGui.GetScrollMaxY() * 0.95f)
                    ImGui.SetScrollHereY(1f);

                ImGui.EndChild();
            ImGui.End();
        }
    }
}
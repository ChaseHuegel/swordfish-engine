using System.Numerics;

using ImGuiNET;

using Swordfish.Engine.Rendering.UI.Elements.Interfaces;
using Swordfish.Library.Diagnostics;

namespace Swordfish.Engine.Rendering.UI.Elements.Diagnostics
{
    public class StatsWindow : Element
    {
        public override void OnUpdate()
        {
            Enabled = Debug.Enabled && Debug.Stats;
        }
 
        public override void OnShow()
        {
            ImGui.SetNextWindowPos(Vector2.Zero);

            ImGui.Begin(Name, WindowFlagPresets.FLAT | ImGuiWindowFlags.NoBringToFrontOnFocus);
                ImGui.Text($"FPS: {Swordfish.MainWindow.FPS}");
                ImGui.Text($"Frame: {Swordfish.Frame}");
                ImGui.Text($"Time: {Swordfish.Time.ToString("0.##")}");
                ImGui.Text($"PingPong: {Swordfish.PingPong.ToString("0.##")}");
                ImGui.Text($"Draw calls: {Swordfish.Renderer.DrawCalls}");
                ImGui.Text($"Timescale: {Swordfish.Timescale.ToString("0.##")}");
                ImGui.Text($"Exposure: {Swordfish.Settings.Renderer.EXPOSURE.ToString("0.##")}");
                ImGui.Text($"Entities: {Swordfish.ECS.EntityCount}");
                ImGui.Text($"Physics");
                    ImGui.Text($"   world: {Swordfish.Physics.WorldSize}");
                    ImGui.Text($"   colliders: {Swordfish.Physics.ColliderCount}");
                    ImGui.Text($"   broad hits: {Swordfish.Physics.BroadCollisions}");
                    ImGui.Text($"   narrow hits: {Swordfish.Physics.NarrowCollisions}");
            ImGui.End();
        }
    }
}
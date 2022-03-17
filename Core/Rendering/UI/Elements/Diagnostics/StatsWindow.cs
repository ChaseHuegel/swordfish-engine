using System.Numerics;

using ImGuiNET;

using Swordfish.Core.Rendering.UI.Elements.Interfaces;
using Swordfish.Library.Diagnostics;

namespace Swordfish.Core.Rendering.UI.Elements.Diagnostics
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
                ImGui.Text($"FPS: {Engine.MainWindow.FPS}");
                ImGui.Text($"Frame: {Engine.Frame}");
                ImGui.Text($"Time: {Engine.Time.ToString("0.##")}");
                ImGui.Text($"PingPong: {Engine.PingPong.ToString("0.##")}");
                ImGui.Text($"Draw calls: {Engine.Renderer.DrawCalls}");
                ImGui.Text($"Timescale: {Engine.Timescale.ToString("0.##")}");
                ImGui.Text($"Exposure: {Engine.Settings.Renderer.EXPOSURE.ToString("0.##")}");
                ImGui.Text($"Entities: {Engine.ECS.EntityCount}");
                ImGui.Text($"Physics");
                    ImGui.Text($"   world: {Engine.Physics.WorldSize}");
                    ImGui.Text($"   colliders: {Engine.Physics.ColliderCount}");
                    ImGui.Text($"   broad hits: {Engine.Physics.BroadCollisions}");
                    ImGui.Text($"   narrow hits: {Engine.Physics.NarrowCollisions}");
            ImGui.End();
        }
    }
}
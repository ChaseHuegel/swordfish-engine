using System.Numerics;

using ImGuiNET;

using Swordfish.Rendering.UI;

namespace Swordfish.Diagnostics
{
    public static class Statistics
    {
        /// <summary>
        /// Dummy method to force construction of the static class
        /// </summary>
        public static void Initialize() { }

        static Statistics()
        {
            Debug.Log("Statistics initialized");
        }

        /// <summary>
        /// Presents the statistics GUI
        /// </summary>
        public static void ShowGui()
        {
            ImGui.SetNextWindowPos(Vector2.Zero);

            ImGui.Begin("Stats", WindowFlagPresets.FLAT);
                ImGui.Text($"FPS: {Engine.MainWindow.FPS}");
                ImGui.Text($"Frame: {Engine.Frame}");
                ImGui.Text($"Draw calls: {Engine.Renderer.DrawCalls}");
                ImGui.Text($"Timescale: {Engine.Timescale.ToString("0.##")}");
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
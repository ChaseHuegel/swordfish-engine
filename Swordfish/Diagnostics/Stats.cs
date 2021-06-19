using System.Numerics;

using ImGuiNET;

using Swordfish.Rendering.UI;

namespace Swordfish.Diagnostics
{
    public static class Statistics
    {
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
                ImGui.Text($"Entities: {Engine.ECS.EntityCount}");

                ImGui.Text($"T - Main: { (Engine.FrameTime*1000f).ToString("0.##") } ms");
                ImGui.Text($"T - ECS: { (Engine.ECS.ThreadTime*1000f).ToString("0.##") } ms");
                ImGui.Text($"T - Physics: { (Engine.Physics.ThreadTime*1000f).ToString("0.##") } ms");
            ImGui.End();
        }
    }
}
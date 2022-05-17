using System;
using System.Numerics;
using ImGuiNET;
using Swordfish.Library.Diagnostics;

namespace Swordfish.Engine.Rendering.UI.Elements.Diagnostics
{
    public class ProfilerWindow : Element
    {
        private const int MinHistory = 60;
        private const int MaxHistory = 2000;

        private Tooltip tooltip = new Tooltip();

        public override void OnUpdate()
        {
            Enabled = Debug.Enabled && Debug.Profiling;
        }

        public override void OnShow()
        {
            ImGui.Begin(Name, WindowFlagPresets.FLAT | ImGuiWindowFlags.NoBringToFrontOnFocus);
            //  Allow scrolling zoom profiler history in/out
            if (ImGui.IsWindowHovered() && Input.GetMouseScroll() != 0)
            {
                Swordfish.Settings.Profiler.HISTORY = Math.Clamp(
                        Swordfish.Settings.Profiler.HISTORY - (int)(Input.GetMouseScroll() * 10),
                        MinHistory,
                        MaxHistory
                    );
            }

            //  Profile data
            float highest, lowest, average;
            float[] profile;

            //  Profile and present Physics
            Profiler.Collect(ProfilerType.PHYSICS, Swordfish.Physics.Thread.DeltaTime, ImGui.IsWindowHovered(), out highest, out lowest, out average, out profile);
            Present("T - Physics", 0f, 16f, profile, highest, lowest, average);

            //  Profile and present ECS
            Profiler.Collect(ProfilerType.ECS, Swordfish.ECS.Thread.DeltaTime, ImGui.IsWindowHovered(), out highest, out lowest, out average, out profile);
            Present("T - ECS", 0f, 16f, profile, highest, lowest, average);

            //  Profile and present Main
            Profiler.Collect(ProfilerType.MAIN, Swordfish.MainWindow.DeltaTime, ImGui.IsWindowHovered(), out highest, out lowest, out average, out profile);
            Present("T - Main", 0f, 16f, profile, highest, lowest, average);

            ImGui.SetWindowPos(new Vector2(0f, Swordfish.Settings.Window.HEIGHT - ImGui.GetWindowHeight()));
            ImGui.End();
        }

        /// <summary>
        /// Present a profiler with provided name and view scaled to min-max range using provided data and optional high, low, average values.
        /// </summary>
        public void Present(string name, float min, float max, float[] profile, float highest = 0f, float lowest = 0f, float average = 0f)
        {
            ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0.25f));
            ImGui.PushStyleColor(ImGuiCol.PlotHistogram, new Vector4(highest / 16f, 16f / average * 8f / highest, 0f, 1f));
            ImGui.PlotHistogram(
                            string.Empty,
                            ref profile[0], profile.Length, 0,
                            name,
                            0f, 16f,
                            new Vector2(Swordfish.Settings.Window.WIDTH * 0.45f, Swordfish.Settings.Window.WIDTH * 0.05f)
                        );
            ImGui.PopStyleColor();
            ImGui.PopStyleColor();

            string stats = $"Average: {average.ToString("0.##")}ms\n"
                         + $"Lowest: {lowest.ToString("0.##")}ms\n"
                         + $"Highest: {highest.ToString("0.##")}ms";

            tooltip.Text = stats;
            tooltip.OnShow();
        }
    }
}
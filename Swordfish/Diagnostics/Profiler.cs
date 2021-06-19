using System.Collections;
using System.Linq;
using System.Numerics;

using ImGuiNET;

using Swordfish.Rendering.UI;

namespace Swordfish.Diagnostics
{
    public static class Profiler
    {
        //  Internal profiler's storage
        private static Queue mainProfile;
        private static Queue ecsProfile;
        private static Queue physicsProfile;

        static Profiler()
        {
            Debug.Log("Profiler initialized");

            mainProfile = new Queue();
            ecsProfile = new Queue();
            physicsProfile = new Queue();

            for (int i = 0; i < Engine.Settings.Profiler.HISTORY; i++)
            {
                mainProfile.Enqueue(0f);
                ecsProfile.Enqueue(0f);
                physicsProfile.Enqueue(0f);
            }
        }

        /// <summary>
        /// Update provided profile with currentTime.
        /// If paused, all info will be collected and output but the profile wont be stepped through.
        /// This should be called every step you are profiling.
        /// Outputs the highest, lowest, and average timings in the profile.
        /// </summary>
        /// <param name="profile"></param>
        /// <param name="currentTime"></param>
        /// <param name="paused">Pause stepping through the profile</param>
        /// <param name="highest"></param>
        /// <param name="lowest"></param>
        /// <param name="average"></param>
        /// <param name="timings"></param>
        public static void Collect(ref Queue profile, float currentTime, bool paused, out float highest, out float lowest, out float average, out float[] timings)
        {
            //  Step through the profile if collection isn't paused
            if (!paused)
            {
                //  Add current thread timing to the profile
                profile.Enqueue(currentTime * 1000f);

                //  Remove the oldest thread timing
                profile.Dequeue();
            }

            //  Collect the highest, lowest, and average timings in the current profile...
            highest = 0f;
            lowest = 999f;
            average = 0f;

            foreach (float value in profile)
            {
                if (value < lowest && value > 0f) lowest = value;
                if (value > highest) highest = value;
                average += value;
            }

            average /= profile.Count;

            //  Cast the queue to an array that can be fed into the UI
            timings = profile.Cast<float>().ToArray();
        }

        /// <summary>
        /// Present a profiler with provided name and view scaled to min-max range using provided data and optional high, low, average values.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="profile"></param>
        /// <param name="highest"></param>
        /// <param name="lowest"></param>
        /// <param name="average"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        public static void Present(string name, float min, float max, float[] profile, float highest = 0f, float lowest = 0f, float average = 0f)
        {
            ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0.25f));
            ImGui.PushStyleColor(ImGuiCol.PlotHistogram, new Vector4(highest/16f, 16f/average * 8f/highest, 0f, 1f));
                ImGui.PlotHistogram(
                                $"A: {(average).ToString("0.##")}\n"
                                + $"L: {(lowest).ToString("0.##")}\n"
                                + $"H: {(highest).ToString("0.##")}",
                                ref profile[0], profile.Length, 0,
                                name,
                                0f, 16f,
                                new Vector2(Engine.Settings.Window.WIDTH*0.45f, Engine.Settings.Window.WIDTH*0.05f)
                            );
            ImGui.PopStyleColor();
            ImGui.PopStyleColor();
        }

        /// <summary>
        /// Presents the internal profiler's GUI
        /// </summary>
        public static void ShowGui()
        {
            ImGui.Begin("Profiler", WindowFlagPresets.FLAT);
                //  Profile data
                float highest, lowest, average;
                float[] profile;

                //  Profile and present Physics
                Collect(ref physicsProfile, Engine.Physics.Thread.DeltaTime, ImGui.IsWindowHovered(),out highest, out lowest, out average, out profile);
                Present("T - Physics", 0f, 16f, profile, highest, lowest, average);

                //  Profile and present ECS
                Collect(ref ecsProfile, Engine.ECS.Thread.DeltaTime, ImGui.IsWindowHovered(),out highest, out lowest, out average, out profile);
                Present("T - ECS", 0f, 16f, profile, highest, lowest, average);

                //  Profile and present Main
                Collect(ref mainProfile, Engine.DeltaTime, ImGui.IsWindowHovered(), out highest, out lowest, out average, out profile);
                Present("T - Main", 0f, 16f, profile, highest, lowest, average);

            ImGui.SetWindowPos(new Vector2(0f, Engine.Settings.Window.HEIGHT - ImGui.GetWindowHeight()));
            ImGui.End();
        }
    }
}
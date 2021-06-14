using System.Net.WebSockets;
using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Numerics;
using OpenTK.Graphics.OpenGL4;
using Swordfish.Rendering;
using ImGuiNET;
using OpenTK.Windowing.Desktop;
using Swordfish.Rendering.UI;

namespace Swordfish
{
    public enum LogType
    {
        INFO,
        WARNING,
        ERROR
    }

    public class LogWriter : TextWriter
    {
        private List<string> lines = new List<string>();

        private TextWriter original;
        public LogWriter(TextWriter original)
        {
            this.original = original;
        }

        public override Encoding Encoding
        {
            get { return Encoding.Default; }
        }

        public override void WriteLine(string value)
        {
            lines.Add(value);

            //  Only write to original writer if this isn't a release build
            if (!Engine.Settings.IS_RELEASE) original.WriteLine(value);
        }

        public List<string> GetLines() => lines;
        public List<string> GetLines(int count) => lines.GetRange(Math.Max(lines.Count-count-1, 0), lines.Count-Math.Max(lines.Count-count-1, 0));
    }

    public class Debug : Singleton<Debug>
    {
        public static void Log(string message, LogType type = LogType.INFO) { Log(message, "", type); }

        public static void Log(string message, string title, LogType type = LogType.INFO, [CallerLineNumber] int lineNumber = 0,
            [CallerMemberName] string caller = null, [CallerFilePath] string callerPath = null,string debugTagging = "")
        {
            if (type == LogType.ERROR || type == LogType.WARNING)
                debugTagging = "\n      at line " + lineNumber + " (" + caller + ") in " + callerPath;

            Console.WriteLine($"[{type.ToString()}] {title}: {message}{debugTagging}");
            // Console.WriteLine($"{DateTime.Now} [{type.ToString()}] {title}: {message}{debugTagging}");
        }

        private static void GLErrorCallback(DebugSource source, DebugType type, int id,
            DebugSeverity severity, int length, IntPtr message, IntPtr userParam)
        {
            Debug.Log(
                (
                    source == DebugSource.DebugSourceApplication ?
                    message.ToString() :
                    $"{message.ToString()} id:{id} severity:{severity} type:{type} source:{source}"
                ),
                "OpenGL",
                LogType.ERROR
                );
        }

        public static bool HasCapabilities(int major, int minor, params string[] extensions)
        {
            string versionString = GL.GetString(StringName.Version);
            Version version = new Version(versionString.Split(' ')[0]);

            return version >= new Version(major, minor) || HasExtensions(extensions);
        }

        public static bool HasExtensions(params string[] extensions)
        {
            List<string> supportedExtensions = GLHelper.GetSupportedExtensions();

            foreach (var extension in extensions)
                if (!supportedExtensions.Contains(extension))
                    return false;

            return true;
        }

        public static bool HasGLOutput()
        {
            return Instance.hasGLOutput;
        }

        public static void TryLogGLError(string title,
            [CallerLineNumber] int lineNumber = 0, [CallerMemberName] string caller = null, [CallerFilePath] string callerPath = null)
        {
            ErrorCode error = GL.GetError();
            if (error != ErrorCode.NoError)
            {
                Debug.Log(error.ToString(), $"OpenGL - {title}", LogType.ERROR, lineNumber, caller, callerPath);
            }
        }

        public static void ShowProfilerGui()
        {
            ImGui.Begin("Profiler", WindowFlagPresets.FLAT);
                //  Profile data
                float highest, lowest, average;
                float[] profile;

                //  Profile and present ECS
                ProfileTimings(ref Instance.ecsProfile, Engine.ECS.Thread.DeltaTime, ImGui.IsWindowHovered(),out highest, out lowest, out average, out profile);

                ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0.25f));
                ImGui.PushStyleColor(ImGuiCol.PlotHistogram, new Vector4(highest/16f, 16f/average * 8f/highest, 0f, 1f));
                    ImGui.PlotHistogram(
                                    $"A: {(average).ToString("0.##")}\n"
                                    + $"L: {(lowest).ToString("0.##")}\n"
                                    + $"H: {(highest).ToString("0.##")}",
                                    ref profile[0], profile.Length, 0,
                                    "T - ECS",
                                    0f, 16f,
                                    new Vector2(Engine.Settings.WINDOW_SIZE.X*0.45f, Engine.Settings.WINDOW_SIZE.X*0.05f)
                                );
                ImGui.PopStyleColor();
                ImGui.PopStyleColor();

                //  Profile and present Main
                ProfileTimings(ref Instance.mainProfile, Engine.DeltaTime, ImGui.IsWindowHovered(), out highest, out lowest, out average, out profile);

                ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0.25f));
                ImGui.PushStyleColor(ImGuiCol.PlotHistogram, new Vector4(highest/16f, 16f/average * 8f/highest, 0f, 1f));
                    ImGui.PlotHistogram(
                                    $"A: {(average).ToString("0.##")}\n"
                                    + $"L: {(lowest).ToString("0.##")}\n"
                                    + $"H: {(highest).ToString("0.##")}",
                                    ref profile[0], profile.Length, 0,
                                    "T - Main",
                                    0f, 16f,
                                    new Vector2(Engine.Settings.WINDOW_SIZE.X*0.45f, Engine.Settings.WINDOW_SIZE.X*0.05f)
                                );
                ImGui.PopStyleColor();
                ImGui.PopStyleColor();
                ImGui.SetWindowPos(new Vector2(0f, Engine.Settings.WINDOW_SIZE.Y - ImGui.GetWindowHeight()));
            ImGui.End();
        }

        public static void ShowStatsGui()
        {
            ImGui.SetNextWindowPos(Vector2.Zero);

            ImGui.Begin("Stats", WindowFlagPresets.FLAT);
                ImGui.Text($"FPS: {Engine.MainWindow.FPS}");
                ImGui.Text($"Frame: {Engine.Frame}");
                ImGui.Text($"Entities: {Engine.ECS.EntityCount}");

                ImGui.Text($"T - Main: { (Engine.FrameTime*1000f).ToString("0.##") } ms");
                ImGui.Text($"T - ECS: { (Engine.ECS.ThreadTime*1000f).ToString("0.##") } ms");
            ImGui.End();

            ShowProfilerGui();
        }

        public static void ShowDebugGui()
        {
            MonitorInfo display = GLHelper.GetPrimaryDisplay();

            ShowStatsGui();

            ImGui.SetNextWindowPos(new Vector2(0, Engine.MainWindow.ClientSize.Y - Engine.MainWindow.ClientSize.Y * 0.2f));
            ImGui.SetNextWindowSize( new Vector2(Engine.MainWindow.ClientSize.X, Engine.MainWindow.ClientSize.Y * 0.2f) );

            ImGui.Begin("Debug", ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoMove);
                ImGui.BeginChild("scrollview", Vector2.Zero, false, ImGuiWindowFlags.AlwaysVerticalScrollbar);

                ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
                    foreach (string line in Instance.writer.GetLines(100))
                        ImGui.TextWrapped(line);
                ImGui.PopStyleVar();

                //  Auto scroll if the bar is at the bottom
                if (ImGui.GetScrollY() >= ImGui.GetScrollMaxY() * 0.95f)
                    ImGui.SetScrollHereY(1f);

                ImGui.EndChild();
            ImGui.End();
        }

        public static void Dump()
        {
            File.WriteAllLines("debug.log", GetWriter().GetLines());
        }

        //  Internal profile timings storage
        protected Queue ecsProfile;
        protected Queue mainProfile;

        /// <summary>
        /// Update provided timing profile with provided currentTime.
        /// Outputs the highest, lowest, and average timings in the profile.
        /// This should be called every update or frame you are profiling.
        /// </summary>
        /// <param name="highest">highest timing in ms</param>
        /// <param name="lowest">lowest timing in ms</param>
        /// <param name="average">average timing in ms</param>
        /// <param name="profile">array of timings in ms</param>
        public static void ProfileTimings(ref Queue profile, float currentTime, bool pause, out float highest, out float lowest, out float average, out float[] timings)
        {
            //  Create an empty ECS profile if it doesn't exist
            if (profile == null)
            {
                profile = new Queue();

                for (int i = 0; i < Engine.Settings.PROFILE_LENGTH; i++)
                    profile.Enqueue(0f);
            }

            if (!pause)
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

        protected LogWriter writer = new LogWriter(Console.Out);
        public static LogWriter GetWriter() { return Instance.writer; }

        protected bool hasGLOutput;
        private DebugProc glErrorDelegate;

        public static bool Enabled = false;
        public static bool Stats = false;

        private Debug()
        {
            Debug.Log("Logger initialized.");

            if (hasGLOutput = HasCapabilities(4, 3, "GL_KHR_debug") == false)
                Debug.Log("...OpenGL debug output is unavailable, manual fallback will be used");
            else
            {
                glErrorDelegate = new DebugProc(GLErrorCallback);
                GL.DebugMessageCallback(glErrorDelegate, IntPtr.Zero);
                Debug.Log("...Created OpenGL debug context");
            }
        }
    }
}
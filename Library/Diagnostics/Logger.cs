using System;
using System.Numerics;
using System.Runtime.CompilerServices;

using ImGuiNET;

using OpenTK.Windowing.Desktop;

using Swordfish.Core;
using Swordfish.Core.Rendering;

namespace Swordfish.Library.Diagnostics
{
    public enum LogType
    {
        NONE,
        INFO,
        WARNING,
        ERROR
    }

    public static class Logger
    {
        public static bool Enabled = false;
        public static bool Stats = false;

        public static bool HasErrors { get; private set; }

        public static LogWriter Writer { get; private set; }

        /// <summary>
        /// Dummy method to force construction of the static class
        /// </summary>
        public static void Initialize() { }

        static Logger()
        {
            //  Create a writer that mirrors to the console
            Writer = new LogWriter(Console.Out);

            //  Set the Console output to the Debug writer
            Console.SetOut(Writer);

            Write("Logger initialized");
        }

        public static void Pad() => Console.WriteLine("");

        public static void Write(string message, LogType type = LogType.INFO, bool snuff = false) => Write(message, "", type);

        public static void Write(string message, string title, LogType type = LogType.INFO, bool snuff = false, [CallerLineNumber] int lineNumber = 0,
            [CallerMemberName] string caller = null, [CallerFilePath] string callerPath = null, string debugTagging = "")
        {
            if (!snuff && (type == LogType.ERROR || type == LogType.WARNING))
            {
                //  Tag on a trace to the error
                debugTagging += "\n      at line " + lineNumber + " (" + caller + ") in " + callerPath;

                // Flag that the debugger has errors
                HasErrors = true;
            }

            if (title != string.Empty)
                title += ": ";

            if (type == LogType.NONE)
                Console.WriteLine($"    {title}{message}{debugTagging}");
            else
                Console.WriteLine($"[{type.ToString()}] {title}{message}{debugTagging}");
            // Console.WriteLine($"{DateTime.Now} [{type.ToString()}] {title}: {message}{debugTagging}");
        }

        /// <summary>
        /// Presents the logger console GUI
        /// </summary>
        public static void ShowGui()
        {
            MonitorInfo display = GLHelper.GetPrimaryDisplay();

            ImGui.SetNextWindowPos(new Vector2(0, Engine.Settings.Window.HEIGHT - Engine.Settings.Window.HEIGHT * 0.2f), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSize( new Vector2(Engine.Settings.Window.WIDTH, Engine.Settings.Window.HEIGHT * 0.2f), ImGuiCond.FirstUseEver);

            ImGui.Begin("Logger");
                ImGui.BeginChild("scrollview", Vector2.Zero, false, ImGuiWindowFlags.AlwaysVerticalScrollbar);

                ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
                    foreach (string line in Writer.GetLines(100))
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
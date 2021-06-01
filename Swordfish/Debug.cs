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
            original.WriteLine(value);
        }

        public List<string> GetLines() => lines;
    }

    public class Debug : Singleton<Debug>
    {
        public static void Log(string message, LogType type = LogType.INFO) { Log(message, "", type); }

        public static void Log(string message, string title, LogType type = LogType.INFO, [CallerLineNumber] int lineNumber = 0,
            [CallerMemberName] string caller = null, [CallerFilePath] string callerPath = null,string debugTagging = "")
        {
            if (type == LogType.ERROR || type == LogType.WARNING)
                debugTagging = "\n      at line " + lineNumber + " (" + caller + ") in " + callerPath;

            Console.WriteLine($"{DateTime.Now} [{type.ToString()}] {title}: {message}{debugTagging}");
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

        public static void ShowDebugGui()
        {
            MonitorInfo display = GLHelper.GetPrimaryDisplay();
            ImGui.SetNextWindowPos(new Vector2(0, 0));
            ImGui.SetNextWindowSize( new Vector2(Engine.MainWindow.ClientSize.X, Engine.MainWindow.ClientSize.Y * 0.2f) );

            ImGui.Begin("Debug", ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoMove);
                ImGui.BeginChild("scrollview", Vector2.Zero, false, ImGuiWindowFlags.AlwaysVerticalScrollbar);

                ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
                    foreach (string line in Instance.writer.GetLines())
                        ImGui.TextWrapped(line);
                ImGui.PopStyleVar();

                //  Auto scroll if the bar is at the bottom
                if (ImGui.GetScrollY() >= ImGui.GetScrollMaxY() * 0.95f)
                    ImGui.SetScrollHereY(1f);

                ImGui.EndChild();
            ImGui.End();
        }

        protected LogWriter writer = new LogWriter(Console.Out);
        public static LogWriter GetWriter() { return Instance.writer; }

        protected bool hasGLOutput;
        private DebugProc glErrorDelegate;

        public static bool Enabled = false;

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

            Debug.TryLogGLError("DebugContext");
        }
    }
}
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Linq;
using OpenTK.Graphics.OpenGL4;
using Swordfish.Rendering;

namespace Swordfish
{
    public enum LogType
    {
        INFO,
        WARNING,
        ERROR
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
            List<string> supportedExtensions = OGL.GetSupportedExtensions();

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

        public bool hasGLOutput;
        private DebugProc glErrorDelegate;
        public Debug()
        {
            Debug.Log("Logger initialized.");

            if (hasGLOutput = HasCapabilities(4, 3, "GL_KHR_debug") == false)
                Debug.Log("OpenGL debug output is unavailable, must use manual fallback");
            else
            {
                glErrorDelegate = new DebugProc(GLErrorCallback);
                GL.DebugMessageCallback(glErrorDelegate, IntPtr.Zero);
                Debug.Log("    Created OpenGL debug context");
            }

            Debug.TryLogGLError("DebugContext");
        }
    }
}
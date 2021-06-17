using System;
using System.IO;
using System.Runtime.CompilerServices;

using OpenTK.Graphics.OpenGL4;

using Swordfish.Rendering;

namespace Swordfish.Diagnostics
{
    public static class Debug
    {
        static Debug()
        {
            Log("Debugger initialized");
        }

        /// <summary>
        /// Enable/disable presenting and consuming debug tools, independent of the console
        /// </summary>
        public static bool Enabled = false;

        /// <summary>
        /// Enable/disable presenting the console, independent of debugging tools
        /// </summary>
        public static bool Console = false;

        /// <summary>
        /// True if GL debug output is supported; otherwise false if manual fallback is must be used
        /// </summary>
        public static bool HasGLOutput { get; private set; }
        private static DebugProc glErrorDelegate;

        /// <summary>
        /// Enable/disable presenting and tracking statistics tool
        /// </summary>
        public static bool Stats
        {
            get => _stats;

            //  Only allow stats to set if debug is enabled
            set { if (Enabled) _stats = value; }
        }
        private static bool _stats = true;

        /// <summary>
        /// Enable/disable presenting and recording the profiler tool
        /// </summary>
        public static bool Profiling
        {
            get => _profiling;

            //  Only allow profiling to set if debug is enabled
            set { if (Enabled) _profiling = value; }
        }
        private static bool _profiling = true;

        /// <summary>
        /// Dump the console to a file
        /// </summary>
        public static void Dump() => File.WriteAllLines("debug.log", Logger.Writer.GetLines());

        /// <summary>
        /// Pushes a message to the logger of optional type
        /// Automatically collects and forwards caller info for debugging.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="type"></param>
        public static void Log(string message, LogType type = LogType.INFO, [CallerLineNumber] int lineNumber = 0,
            [CallerMemberName] string caller = null, [CallerFilePath] string callerPath = null)
        {
            Logger.Write(message, "", type, lineNumber, caller, callerPath);
        }

        /// <summary>
        /// Pushes a message with a title to the logger of optional type.
        /// Automatically collects and forwards caller info for debugging.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="title"></param>
        /// <param name="type"></param>
        /// <param name="lineNumber"></param>
        /// <param name="caller"></param>
        /// <param name="callerPath"></param>
        /// <param name="debugTagging"></param>
        public static void Log(string message, string title, LogType type = LogType.INFO, [CallerLineNumber] int lineNumber = 0,
            [CallerMemberName] string caller = null, [CallerFilePath] string callerPath = null)
        {
            Logger.Write(message, title, type, lineNumber, caller, callerPath);
        }

        /// <summary>
        /// Consumes the most recent GL error (if any) and pushes to the logger with specified title.
        /// Automatically collects and forwards caller info for debugging.
        /// </summary>
        /// <param name="title"></param>
        /// <param name="lineNumber"></param>
        /// <param name="caller"></param>
        /// <param name="callerPath"></param>
        /// <returns>True if an error was collected; otherwise false</returns>
        public static bool TryCollectGLError(string title,
            [CallerLineNumber] int lineNumber = 0, [CallerMemberName] string caller = null, [CallerFilePath] string callerPath = null)
        {
            ErrorCode error = GL.GetError();
            if (error != ErrorCode.NoError)
            {
                Logger.Write(error.ToString(), $"OpenGL - {title}", LogType.ERROR, lineNumber, caller, callerPath);

                return true;
            }

            return false;
        }

        /// <summary>
        /// Consumes all GL errors (if any) and pushes to the logger with specified title.
        /// Automatically collects and forwards caller info for debugging.
        /// </summary>
        /// <param name="title"></param>
        /// <param name="lineNumber"></param>
        /// <param name="caller"></param>
        /// <param name="callerPath"></param>
        /// <returns>True if any errors were collected; otherwise false</returns>
        public static bool TryCollectAllGLErrors(string title,
            [CallerLineNumber] int lineNumber = 0, [CallerMemberName] string caller = null, [CallerFilePath] string callerPath = null)
        {
            bool hadError = false;

            ErrorCode error = GL.GetError();
            while (error != ErrorCode.NoError)
            {
                Logger.Write(error.ToString(), $"OpenGL - {title}", LogType.ERROR, lineNumber, caller, callerPath);
                error = GL.GetError();
                hadError = true;
            }

            return hadError;
        }

        /// <summary>
        /// Attempts to create a callback for GL debug output.
        /// Requires the driver to have extension GL_KHR_DEBUG v4.3 or higher
        /// </summary>
        /// <returns>True if the callback was created; otherwise false if manual fallback must be used (i.e. TryCollectGLError)</returns>
        public static bool TryCreateGLOutput()
        {
            if (HasGLOutput = GLHelper.HasCapabilities(4, 3, "GL_KHR_debug") == false)
            {
                Debug.Log("...OpenGL debug output is unavailable, manual fallback will be used");
            }
            else
            {
                glErrorDelegate = new DebugProc(GLErrorCallback);
                GL.DebugMessageCallback(glErrorDelegate, IntPtr.Zero);

                Debug.Log("...Created OpenGL debug output");
            }

            return (glErrorDelegate == null);
        }

        /// <summary>
        /// OpenGL callback to push GL errors to the logger
        /// </summary>
        /// <param name="source"></param>
        /// <param name="type"></param>
        /// <param name="id"></param>
        /// <param name="severity"></param>
        /// <param name="length"></param>
        /// <param name="message"></param>
        /// <param name="userParam"></param>
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

        /// <summary>
        /// Present all enabled debug GUIs
        /// </summary>
        public static void ShowGui()
        {
            if (Stats) Statistics.ShowGui();
            if (Profiling) Profiler.ShowGui();
        }
    }
}
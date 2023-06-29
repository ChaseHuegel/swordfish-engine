using System.Runtime.InteropServices;
using Silk.NET.OpenGL;
using Swordfish.Graphics.SilkNET.OpenGL;
using Swordfish.Library.Diagnostics;

namespace Swordfish.Util
{
    public static class GLDebug
    {
        private static GL GL => gl ??= SwordfishEngine.Kernel.Get<GL>();
        private static GL gl;

        private static DebugProc glErrorDelegate;

        /// <summary>
        /// True if OpenGL debug output is supported on the current system; otherwise false.
        /// </summary>
        public static bool HasGLOutput { get; private set; }

        /// <summary>
        /// Consumes the most recent GL error (if any) and pushes to the logger with specified title.
        /// Automatically collects and forwards caller info for debugging.
        /// </summary>
        /// <param name="title"></param>
        /// <param name="lineNumber"></param>
        /// <param name="caller"></param>
        /// <param name="callerPath"></param>
        /// <returns>True if an error was collected; otherwise false</returns>
        public static bool TryCollectGLError(string title)
        {
            if (HasGLOutput)
                return false;

            GLEnum error = GL.GetError();
            if (error != GLEnum.NoError)
            {
                Logger.Write(error.ToString(), $"OpenGL - {title}", LogType.ERROR, true, false);

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
        public static bool TryCollectAllGLErrors(string title)
        {
            if (HasGLOutput)
                return false;

            bool hadError = false;

            GLEnum error = GL.GetError();
            while (error != GLEnum.NoError)
            {
                Logger.Write(error.ToString(), $"OpenGL - {title}", LogType.ERROR, true, false);
                error = GL.GetError();
                hadError = true;
            }

            return hadError;
        }

        public static bool TryCreateGLOutput()
        {
            HasGLOutput = GL.HasCapabilities(4, 3, "GL_KHR_debug");

            if (!HasGLOutput)
            {
                glErrorDelegate = new DebugProc(GLErrorCallback);
                GL.DebugMessageCallback(glErrorDelegate, IntPtr.Zero);
                GL.Enable(EnableCap.DebugOutput);
                GL.Enable(EnableCap.DebugOutputSynchronous);
            }

            return HasGLOutput;
        }

        private static void GLErrorCallback(GLEnum source, GLEnum type, int id, GLEnum severity, int length, nint message, nint userParam)
        {
            string output = Marshal.PtrToStringAnsi(message, length);

            LogType logType = LogType.WARNING;

            if (type == GLEnum.DebugTypeError)
                logType = LogType.ERROR;
            else
                return;

            Debugger.Log(output, "OpenGL", logType, true, true);
        }
    }
}

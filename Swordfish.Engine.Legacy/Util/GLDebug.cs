using System;
using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;
using Swordfish.Library.Diagnostics;

namespace Swordfish.Engine.Util
{
    public static class GLDebug
    {
        private static DebugProc glErrorDelegate;

        /// <summary>
        /// True if GL debug output is supported; otherwise false if manual fallback is must be used
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

            ErrorCode error = GL.GetError();
            if (error != ErrorCode.NoError)
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

            ErrorCode error = GL.GetError();
            while (error != ErrorCode.NoError)
            {
                Logger.Write(error.ToString(), $"OpenGL - {title}", LogType.ERROR, true, false);
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
            if ((HasGLOutput = GLHelper.HasCapabilities(4, 3, "GL_KHR_debug")) == false)
            {
                Debugger.Log("...OpenGL debug output is unavailable, manual fallback will be used", LogType.WARNING, false, true);
            }
            else
            {
                glErrorDelegate = new DebugProc(GLErrorCallback);
                GL.DebugMessageCallback(glErrorDelegate, IntPtr.Zero);
                GL.Enable(EnableCap.DebugOutput);
                GL.Enable(EnableCap.DebugOutputSynchronous);

                Debugger.Log("...Created OpenGL debug output");
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
            string output = Marshal.PtrToStringAnsi(message, length);

            LogType logType = LogType.WARNING;

            if (type == DebugType.DebugTypeError)
                logType = LogType.ERROR;

            Debugger.Log(output, "OpenGL", logType, true, true);
        }
    }
}

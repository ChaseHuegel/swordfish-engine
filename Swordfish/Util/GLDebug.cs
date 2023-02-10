using Ninject;
using Silk.NET.OpenGL;
using Swordfish.Library.Diagnostics;

namespace Swordfish.Util
{
    public static class GLDebug
    {
        private static GL GL => gl ??= SwordfishEngine.Kernel.Get<GL>();
        private static GL gl;

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
    }
}

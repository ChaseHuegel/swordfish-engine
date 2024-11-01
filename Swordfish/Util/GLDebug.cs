using System.Runtime.InteropServices;
using DryIoc.ImTools;
using Silk.NET.OpenGL;
using Swordfish.Graphics.SilkNET.OpenGL;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.Util;

namespace Swordfish.Util
{
    public static class GLDebug
    {
        private static GL GL => _gl ??= SwordfishEngine.Kernel.Get<GL>();
        private static GL? _gl;

        /// <summary>
        /// True if OpenGL debug output is supported on the current system; otherwise false.
        /// </summary>
        public static bool HasGLOutput { get; private set; }

        /// <summary>
        /// Collects the most recent GL error, if there is one.
        /// </summary>
        public static Result<GLEnum> CollectGLError()
        {
            if (HasGLOutput)
            {
                return new Result<GLEnum>(success: false, default);
            }

            GLEnum error = GL.GetError();
            if (error == GLEnum.NoError)
            {
                return new Result<GLEnum>(success: false, default);
            }

            return new Result<GLEnum>(success: true, error);
        }

        /// <summary>
        /// Collects all GL errors, if there are any.
        /// </summary>
        public static Result<GLEnum[]> TryCollectAllGLErrors()
        {
            if (HasGLOutput)
            {
                return new Result<GLEnum[]>(success: false, []);
            }

            var errors = new List<GLEnum>();
            
            GLEnum error = GL.GetError();
            while (error != GLEnum.NoError)
            {
                errors.Add(error);
                error = GL.GetError();
            }

            return new Result<GLEnum[]>(errors.Count > 0, errors.ToArray());
        }

        public static bool TryCreateGLOutput(DebugProc debugProc)
        {
            if (HasGLOutput)
            {
                return true;
            }
            
            HasGLOutput = GL.HasCapabilities(4, 3, "GL_KHR_debug");
            if (!HasGLOutput)
            {
                return false;
            }

            GL.DebugMessageCallback(debugProc, IntPtr.Zero);
            GL.Enable(EnableCap.DebugOutput);
            GL.Enable(EnableCap.DebugOutputSynchronous);
            return true;
        }
    }
}

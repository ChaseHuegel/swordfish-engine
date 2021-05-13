using System;
using OpenTK.Graphics.OpenGL4;

namespace Swordfish
{
    public static class Debug
    {
        public static void Log(string message)
        {
            Console.WriteLine(message);
        }

        public static void TryLogGLError(string title)
        {
            var error = GL.GetError();
            if (error != ErrorCode.NoError)
            {
                Debug.Log($"{title}: {error}");
            }
        }
    }
}
using System;
using OpenTK.Graphics.OpenGL4;

namespace Swordfish
{
    public enum LogType
    {
        INFO,
        WARNING,
        ERROR
    }

    public static class Debug
    {
        public static void Log(string message, LogType type = LogType.INFO) { Log(message, "", type); }
        public static void Log(string message, string title, LogType type = LogType.INFO)
        {
            Console.WriteLine($"{DateTime.Now} [{type.ToString()}] {title}: " + message);
        }

        public static void TryLogGLError(string title)
        {
            var error = GL.GetError();
            if (error != ErrorCode.NoError)
            {
                Debug.Log(error.ToString(), title, LogType.ERROR);
            }
        }
    }
}
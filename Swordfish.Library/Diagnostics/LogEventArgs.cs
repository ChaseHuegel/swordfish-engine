using System;
namespace Swordfish.Library.Diagnostics
{
    public class LogEventArgs : EventArgs
    {
        public readonly string Line;
        public readonly string Message;
        public readonly string Title;
        public readonly LogType Type;

        public LogEventArgs(
            string line,
            string message,
            string title,
            LogType type = LogType.INFO)
        {
            Line = line;
            Message = message;
            Title = title;
            Type = type;
        }
    }
}
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using Swordfish.Library.Extensions;

namespace Swordfish.Library.Diagnostics
{
    public enum LogType
    {
        NONE,
        INFO,
        WARNING,
        ERROR,
        CONTINUED
    }

    public static class Logger
    {
        public static EventHandler<LogEventArgs> Logged;

        public static List<LogEventArgs> History { get; }

        public static bool HasErrors { get; private set; }

        internal static LogWriter Writer { get; private set; }

        /// <summary>
        /// Dummy method to force construction of the static class
        /// </summary>
        public static void Initialize() { }

        static Logger()
        {
            //  Create a writer that mirrors the console
            Writer = new LogWriter(Console.Out);
            History = new List<LogEventArgs>();
            Logged += (sender, args) => History.Add(args);

            Write("Logger initialized.");
        }

        public static List<string> GetLines()
        {
            return Writer.GetLines();
        }

        public static void Pad()
        {
            Writer.WriteLine();
            Logged?.Invoke(Writer, new LogEventArgs(Writer.NewLine, Writer.NewLine, string.Empty, LogType.NONE));
        }

        public static void Write(string message, LogType type = LogType.INFO, bool timestamp = false, bool snuff = false, StackTrace trace = null) => Write(message, string.Empty, type);

        public static void Write(string message, string title, LogType type = LogType.INFO, bool timestamp = false, bool snuff = false, StackTrace trace = null)
        {
            if (type == LogType.ERROR || type == LogType.WARNING)
            {
                if (!snuff)
                {
                    if (trace == null)
                        trace = new StackTrace(2, true);

                    if (type == LogType.ERROR)
                        message += trace.GetFrames().Take(5).ToFormattedString();
                    else
                        message += trace.GetFrames().Take(1).ToFormattedString();
                }

                // Flag that the debugger has errors
                HasErrors = true;
            }

            if (!string.IsNullOrEmpty(title))
                title += ": ";

            string output = $"{title}{message}";

            if (timestamp)
                output = $"[{DateTime.Now}] {output}";

            switch (type)
            {
                case LogType.NONE:
                    //  Don't modify the output
                    break;
                case LogType.CONTINUED:
                    output = $"\t{output}";
                    break;
                default:
                    output = $"[{type}] {output}";
                    break;
            }

            Writer.WriteLine(output);
            Logged?.Invoke(Writer, new LogEventArgs(output, message, title, type));
        }
    }
}
using System;
using System.Runtime.CompilerServices;

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

            Write("Logger initialized.");
        }

        public static void Pad() => Writer.WriteLine();

        public static void Write(string message, LogType type = LogType.INFO, bool timestamp = false, bool snuff = false) => Write(message, "", type);

        public static void Write(string message, string title, LogType type = LogType.INFO, bool timestamp = false, bool snuff = false, [CallerLineNumber] int lineNumber = 0,
            [CallerMemberName] string caller = null, [CallerFilePath] string callerPath = null)
        {
            if (!snuff && (type == LogType.ERROR || type == LogType.WARNING))
            {
                //  Tag on a trace to the error
                message += "\n      at line " + lineNumber + " (" + caller + ") in " + callerPath;

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
                    output = $"    {output}";
                    break;
                default:
                    output = $"[{type.ToString()}] {output}";
                    break;
            }

            Writer.WriteLine(output);
        }
    }
}
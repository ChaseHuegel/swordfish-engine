using System.Runtime.InteropServices;
using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace Swordfish.Library.Diagnostics
{
    public static class Debug
    {
        /// <summary>
        /// Dummy method to force construction of the static class
        /// </summary>
        public static void Initialize() { }

        static Debug()
        {
            Logger.Initialize();
            Statistics.Initialize();
            Profiler.Initialize();

            Log("Debugger initialized");
        }

        /// <summary>
        /// Whether any errors have been logged.
        /// </summary>
        public static bool HasErrors => Logger.HasErrors;

        /// <summary>
        /// Enable/disable presenting and consuming debug tools, independent of the console
        /// </summary>
        public static bool Enabled = false;

        /// <summary>
        /// Enable/disable presenting the console, independent of debugging tools
        /// </summary>
        public static bool Console = false;

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
        /// Tell the logger to push an empty line
        /// </summary>
        public static void PadLog() => Logger.Pad();

        /// <summary>
        /// Pushes a message to the logger of optional type
        /// Automatically collects and forwards caller info for debugging.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="type"></param>
        public static void Log(object message, LogType type = LogType.INFO, bool timestamp = false, bool snuff = false, [CallerLineNumber] int lineNumber = 0,
            [CallerMemberName] string caller = null, [CallerFilePath] string callerPath = null)
        {
            Logger.Write(message.ToString(), "", type, timestamp, snuff, lineNumber, caller, callerPath);
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
        public static void Log(object message, string title, LogType type = LogType.INFO, bool timestamp = false, bool snuff = false, [CallerLineNumber] int lineNumber = 0,
            [CallerMemberName] string caller = null, [CallerFilePath] string callerPath = null)
        {
            Logger.Write(message.ToString(), title, type, timestamp, snuff, lineNumber, caller, callerPath);
        }
    }
}
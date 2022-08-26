using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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

            Log("Debugger initialized.");
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

        /// <summary>
        /// Pushes an error message to the logger
        /// Automatically collects and forwards caller info for debugging.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="type"></param>
        public static void LogError(object message, Exception exception, bool timestamp = false, bool snuff = false, [CallerLineNumber] int lineNumber = 0,
            [CallerMemberName] string caller = null, [CallerFilePath] string callerPath = null)
        {
            string output = message.ToString();
            if (exception != null)
                output += Environment.NewLine + exception.ToString();

            Logger.Write(output, "", LogType.ERROR, timestamp, snuff, lineNumber, caller, callerPath);
        }

        /// <summary>
        /// Tries to run an action, catching and logging exceptions.
        /// </summary>
        /// <param name="action">The action to run.</param>
        /// <param name="message">Optional message to log if an exception was caught.</param>
        /// <returns>True if successful; otherwise false.</returns>
        public static bool TryInvoke(Action action, string message = null)
        {
            try
            {
                action.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                LogError(message ?? $"Caught error invoking {action.Method}", ex);
                return false;
            }
        }

        /// <summary>
        /// Tries to run a function with a return type T, catching and logging exceptions.
        /// </summary>
        /// <param name="func">The function to run.</param>
        /// <param name="result">The result of the function if successful; otherwise default.</param>
        /// <param name="message">Optional message to log if an exception was caught.</param>
        /// <typeparam name="T">The return type of the function.</typeparam>
        /// <returns></returns>
        public static bool TryCatch<T>(Func<T> func, out T result, string message = null)
        {
            try
            {
                result = func.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                LogError(message ?? $"Caught error invoking {func.Method}", ex);
                result = default;
                return false;
            }
        }
    }
}
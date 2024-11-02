using Microsoft.Extensions.Logging;

namespace Swordfish.Library.Diagnostics;

public struct LogEventArgs(string categoryName, LogLevel logLevel, string log)
{
    public readonly string Log = log;

    public readonly LogLevel LogLevel = logLevel;

    public readonly string CategoryName = categoryName;
}
using Microsoft.Extensions.Logging;
// ReSharper disable UnusedMember.Global

namespace Swordfish.Library.Diagnostics;

public struct LogEventArgs(in string categoryName, in LogLevel logLevel, in string log)
{
    public readonly string Log = log;

    public readonly LogLevel LogLevel = logLevel;

    public readonly string CategoryName = categoryName;
}
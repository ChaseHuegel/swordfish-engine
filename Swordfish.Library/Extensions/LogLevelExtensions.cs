using System;
using System.Drawing;
using Microsoft.Extensions.Logging;

namespace Swordfish.Library.Extensions;

public static class LogLevelExtensions
{
    public static Color GetColor(this LogLevel logType)
    {
        return logType switch
        {
            LogLevel.Debug => Color.Gray,
            LogLevel.Information => Color.White,
            LogLevel.Warning => Color.Yellow,
            LogLevel.Error => Color.Red,
            LogLevel.Critical => Color.Red,
            LogLevel.Trace => Color.Orange,
            LogLevel.None => Color.Gray,
            _ => throw new ArgumentOutOfRangeException(nameof(logType), logType, null),
        };
    }
}
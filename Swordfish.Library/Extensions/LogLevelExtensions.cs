using System;
using System.Drawing;
using Microsoft.Extensions.Logging;
using Swordfish.Library.Diagnostics;

namespace Swordfish.Library.Extensions
{
    public static class LogLevelExtensions
    {
        public static Color GetColor(this LogLevel logType)
        {
            switch (logType)
            {
                case LogLevel.Debug:
                    return Color.Gray;
                case LogLevel.Information:
                    return Color.White;
                case LogLevel.Warning:
                    return Color.Yellow;
                case LogLevel.Error:
                    return Color.Red;
                case LogLevel.Critical:
                    return Color.Red;
                case LogLevel.Trace:
                    return Color.Orange;
                case LogLevel.None:
                    return Color.Gray;
                default:
                    throw new ArgumentOutOfRangeException(nameof(logType), logType, null);
            }
        }
    }
}

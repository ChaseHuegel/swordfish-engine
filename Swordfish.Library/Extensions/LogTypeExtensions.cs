using System;
using System.Drawing;
using Swordfish.Library.Diagnostics;

namespace Swordfish.Library.Extensions
{
    public static class LogTypeExtensions
    {
        public static Color GetColor(this LogType logType)
        {
            switch (logType)
            {
                case LogType.NONE:
                    return Color.Gray;
                case LogType.INFO:
                    return Color.White;
                case LogType.WARNING:
                    return Color.Yellow;
                case LogType.ERROR:
                    return Color.Red;
                case LogType.CONTINUED:
                    return Color.White;
                default:
                    throw new NotImplementedException();
            }
        }
    }
}

using System.Numerics;

namespace WaywardBeyond.Client.UI;

internal static partial class Widgets
{
    /// <summary>Creates a colour from 0-255 channels.</summary>
    private static Vector4 FromRgb(byte r, byte g, byte b, byte a = 255)
    {
        return new Vector4(r / 255f, g / 255f, b / 255f, a / 255f);
    }
}

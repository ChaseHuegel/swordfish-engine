using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Reef;

/// <summary>
///     A pixel with one byte per channel in RGBA order. The layout is four
///     sequential bytes on every platform.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct PixelRgba8
{
    public readonly byte R;
    public readonly byte G;
    public readonly byte B;
    public readonly byte A;

    public PixelRgba8(byte r, byte g, byte b, byte a)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    /// <summary>
    ///     Converts a color to bytes, rounding each channel to the nearest level.
    /// </summary>
    public static PixelRgba8 FromVector4(Vector4 color)
    {
        return new PixelRgba8(ToByte(color.X), ToByte(color.Y), ToByte(color.Z), ToByte(color.W));
    }

    /// <summary>
    ///     Converts the stored bytes to a color in the zero to one range.
    /// </summary>
    public Vector4 ToVector4()
    {
        return new Vector4(R / 255f, G / 255f, B / 255f, A / 255f);
    }

    internal static byte ToByte(float value)
    {
        return (byte)(Math.Clamp(value, 0f, 1f) * 255f + 0.5f);
    }
}

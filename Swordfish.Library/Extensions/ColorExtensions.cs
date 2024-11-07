using System.Drawing;
using System.Numerics;
// ReSharper disable UnusedMember.Global

namespace Swordfish.Library.Extensions;

public static class ColorExtensions
{
    public static Vector4 ToVector4(this Color color)
    {
        return new Vector4(
            (float)color.R / byte.MaxValue,
            (float)color.G / byte.MaxValue,
            (float)color.B / byte.MaxValue,
            (float)color.A / byte.MaxValue
        );
    }

    public static Vector3 ToVector3(this Color color)
    {
        return new Vector3(
            (float)color.R / byte.MaxValue,
            (float)color.G / byte.MaxValue,
            (float)color.B / byte.MaxValue
        );
    }
}
using System.Drawing;
using System.Numerics;

namespace Swordfish.Library.Extensions
{
    public static class ColorExtensions
    {
        public static Vector4 ToVector4(this Color color)
        {
            return new Vector4(
                color.R / byte.MaxValue,
                color.G / byte.MaxValue,
                color.B / byte.MaxValue,
                color.A / byte.MaxValue
            );
        }

        public static Vector3 ToVector3(this Color color)
        {
            return new Vector3(
                color.R / byte.MaxValue,
                color.G / byte.MaxValue,
                color.B / byte.MaxValue
            );
        }
    }
}
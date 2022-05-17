using System;
using OpenTK.Mathematics;
using Swordfish.Engine.Types;

namespace Swordfish.Engine.Extensions
{
    public static class ColorExtensions
    {
        public static float Grayscale(this Vector4 color) => (color.X + color.Y + color.Z) / 3;

        public static float Grayscale(this Color color) => (color.r + color.g + color.b) / 3;
    }
}

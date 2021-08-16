using System;
using OpenTK.Mathematics;

namespace Swordfish.Types
{
    public static class Color
    {
        public static Vector4 White = new Vector4(1f, 1f, 1f, 1f);
        public static Vector4 Black = new Vector4(0f, 0f, 0f, 1f);

        public static Vector4 Gray = new Vector4(0.5f, 0.5f, 0.5f, 0.5f);
        public static Vector4 Grey => Gray;

        public static Vector4 Red = new Vector4(1f, 0f, 0f, 1f);
        public static Vector4 Green = new Vector4(0f, 1f, 0f, 1f);
        public static Vector4 Blue = new Vector4(0f, 0f, 1f, 1f);
    }
}

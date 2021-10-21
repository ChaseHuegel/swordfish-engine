using OpenTK.Mathematics;
using Swordfish.Rendering;

namespace Swordfish.Extensions
{
    public static class VectorExtensions
    {
        public static System.Numerics.Vector2 ToSysVector(this Vector2 vec) => new System.Numerics.Vector2(vec.X, vec.Y);
        public static Vector2 ToGLVector(this System.Numerics.Vector2 vec) => new Vector2(vec.X, vec.Y);
    }
}

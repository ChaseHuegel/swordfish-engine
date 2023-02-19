using System.Numerics;

namespace Swordfish.Library.Extensions
{
    public static class VectorExtensions
    {
        public static float GetRatio(this Vector2 vector2)
        {
            return vector2.X / vector2.Y;
        }
    }
}

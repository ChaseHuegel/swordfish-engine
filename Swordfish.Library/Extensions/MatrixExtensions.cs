using System.Numerics;

namespace Swordfish.Library.Extensions
{
    public static class MatrixExtensions
    {
        public static Vector3 GetPosition(this Matrix4x4 matrix)
        {
            return new Vector3(matrix.M41, matrix.M42, matrix.M43);
        }
    }
}

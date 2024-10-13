using System.Numerics;

namespace Swordfish.Library.Extensions
{
    public static class VectorExtensions
    {
        public static float GetRatio(this Vector2 vector2)
        {
            return vector2.X / vector2.Y;
        }

        public static Quaternion ToLookRotation(this Vector3 forward)
        {
            return ToLookRotation(forward, Vector3.UnitY);
        }

        public static Quaternion ToLookRotation(this Vector3 forward, Vector3 up)
        {
            forward = Vector3.Normalize(forward);
            up = Vector3.Normalize(up);

            Vector3 right = Vector3.Normalize(Vector3.Cross(up, forward));
            up = Vector3.Cross(forward, right);

            Matrix4x4 rotationMatrix = new Matrix4x4
            (
                right.X, up.X, forward.X, 0,
                right.Y, up.Y, forward.Y, 0,
                right.Z, up.Z, forward.Z, 0,
                0, 0, 0, 1
            );

            return Quaternion.CreateFromRotationMatrix(rotationMatrix);
        }

        public static Quaternion LookAt(this Vector3 position, Vector3 eye)
        {
            return LookAt(position, eye, Vector3.UnitY);
        }

        public static Quaternion LookAt(this Vector3 position, Vector3 eye, Vector3 up)
        {
            Vector3 forward = Vector3.Normalize(position - eye);

            Vector3 right = Vector3.Normalize(Vector3.Cross(up, forward));
            up = Vector3.Cross(forward, right);

            Matrix4x4 rotationMatrix = new Matrix4x4(
                right.X, up.X, forward.X, 0,
                right.Y, up.Y, forward.Y, 0,
                right.Z, up.Z, forward.Z, 0,
                0, 0, 0, 1);

            return Quaternion.CreateFromRotationMatrix(rotationMatrix);
        }
    }
}

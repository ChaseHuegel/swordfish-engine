using System;
using System.Numerics;

namespace Swordfish.Library.Extensions
{
    public static class VectorExtensions
    {
        public static float GetRatio(this Vector2 vector2)
        {
            return vector2.X / vector2.Y;
        }

        public static Quaternion ToLookOrientation(this Vector3 forward)
        {
            return ToLookOrientation(forward, Vector3.UnitY);
        }

        public static Quaternion ToLookOrientation(this Vector3 forward, Vector3 up)
        {
            var right = Vector3.Cross(forward, up);
            return ToLookOrientation(forward, up, right);
        }

        //  Based on LookAt: https://www.opengl-tutorial.org/intermediate-tutorials/tutorial-17-quaternions/
        public static Quaternion ToLookOrientation(this Vector3 forward, Vector3 up, Vector3 right)
        {
            forward = Vector3.Normalize(forward);
            up = Vector3.Normalize(up);
            right = Vector3.Normalize(right);

            Vector3 orthogonalUp = Vector3.Cross(right, forward);

            Quaternion forwardOrientation = Vector3.UnitZ.OrientateTo(forward);

            Vector3 newUp = Vector3.Transform(up, forwardOrientation);
            Quaternion upOrientation = newUp.OrientateTo(orthogonalUp);

            return upOrientation * forwardOrientation;
        }

        //  Based on: https://stackoverflow.com/a/11741520
        public static Quaternion OrientateTo(this Vector3 start, Vector3 desired)
        {
            start = Vector3.Normalize(start);
            desired = Vector3.Normalize(desired);

            float k_cos_theta = Vector3.Dot(start, desired);
            float k = (float)Math.Sqrt(start.LengthSquared() * desired.LengthSquared());

            if (k_cos_theta / k == -1)
            {
                // 180 degree rotation around any orthogonal vector
                var orthogonal = Vector3.Normalize(start.GetMostOrthogonalBasis());
                return new Quaternion(orthogonal, 0);
            }

            float scalar = k_cos_theta + k;
            var quaternion = new Quaternion(Vector3.Cross(start, desired), scalar);
            return Quaternion.Normalize(quaternion);
        }

        public static Vector3 GetMostOrthogonalBasis(this Vector3 vector)
        {
            // Based on: https://stackoverflow.com/a/11741520
            float x = Math.Abs(vector.X);
            float y = Math.Abs(vector.Y);
            float z = Math.Abs(vector.Z);

            Vector3 other = x < y ? (x < z ? Vector3.UnitX : Vector3.UnitZ) : (y < z ? Vector3.UnitY : Vector3.UnitZ);
            return Vector3.Cross(vector, other);
        }

        public static Quaternion LookAt(this Vector3 position, Vector3 eye)
        {
            return LookAt(position, eye, Vector3.UnitY);
        }

        public static Quaternion LookAt(this Vector3 eye, Vector3 position, Vector3 up)
        {
            return (eye - position).ToLookOrientation(up);
        }
    }
}

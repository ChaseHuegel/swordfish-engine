using System.Numerics;
using System.Runtime.CompilerServices;
using Swordfish.Library.Util;

namespace Swordfish.Library.Types
{
    public class Transform
    {
        public Vector3 Position
        {
            get => position;
            set
            {
                dirty = true;
                position = value;
            }
        }

        public Quaternion Rotation
        {
            get => rotation;
            set
            {
                dirty = true;
                rotation = value;
            }
        }

        public Vector3 Scale
        {
            get => scale;
            set
            {
                dirty = true;
                scale = value;
            }
        }

        private bool dirty = true;
        private Vector3 position;
        private Quaternion rotation = Quaternion.Identity;
        private Vector3 scale = Vector3.One;
        private Matrix4x4 matrix4x4;

        public Vector3 GetForward()
        {
            return Vector3.Transform(Vector3.UnitZ, rotation);
        }

        public Vector3 GetRight()
        {
            return Vector3.Transform(Vector3.UnitX, rotation);
        }

        public Vector3 GetUp()
        {
            return Vector3.Transform(Vector3.UnitY, rotation);
        }

        public void Translate(Vector3 translation)
        {
            Position += translation;
        }

        public void Rotate(Vector3 rotation, bool local = false)
        {
            Quaternion eulerQuaternion = Quaternion.CreateFromYawPitchRoll(rotation.Y * MathS.DegreesToRadians, rotation.X * MathS.DegreesToRadians, rotation.Z * MathS.DegreesToRadians);
            if (local)
            {
                Rotation = Quaternion.Multiply(this.rotation, eulerQuaternion);
            }
            else
            {
                Rotation = Quaternion.Multiply(eulerQuaternion, this.rotation);
            }
        }

        public void Scalar(Vector3 scale)
        {
            Scale *= scale;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Matrix4x4 ToMatrix4x4()
        {
            if (!dirty)
                return matrix4x4;

            Vector3 forward = GetForward();
            Vector3 up = GetUp();
            Vector3 right = GetRight();

            Matrix4x4 matrix = new Matrix4x4
            {
                M11 = right.X * scale.X,
                M12 = right.Y * scale.X,
                M13 = right.Z * scale.X,
                M14 = 0,

                M21 = up.X * scale.Y,
                M22 = up.Y * scale.Y,
                M23 = up.Z * scale.Y,
                M24 = 0,

                M31 = forward.X * scale.Z,
                M32 = forward.Y * scale.Z,
                M33 = forward.Z * scale.Z,
                M34 = 0,

                M41 = position.X,
                M42 = position.Y,
                M43 = position.Z,
                M44 = 1
            };

            matrix4x4 = matrix;
            dirty = false;
            return matrix;
        }
    }
}

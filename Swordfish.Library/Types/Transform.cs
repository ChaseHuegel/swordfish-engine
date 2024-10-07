using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using Swordfish.Library.Util;
using MemorizedCos = FastMath.MemoizedCos;
using MemorizedSin = FastMath.MemoizedSin;

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

        public Vector3 Rotation
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
        private Vector3 rotation;
        private Vector3 scale = Vector3.One;
        private Matrix4x4 matrix4x4;

        public Vector3 GetForward()
        {
            //  TODO this is expensive
            var orientation = Quaternion.CreateFromYawPitchRoll(Rotation.Y * MathS.DegreesToRadians, Rotation.X * MathS.DegreesToRadians, Rotation.Z * MathS.DegreesToRadians);
            return Vector3.Transform(Vector3.UnitZ, orientation);
        }

        public Vector3 GetRight()
        {
            //  TODO this is expensive
            var orientation = Quaternion.CreateFromYawPitchRoll(Rotation.Y * MathS.DegreesToRadians, Rotation.X * MathS.DegreesToRadians, Rotation.Z * MathS.DegreesToRadians);
            return Vector3.Transform(Vector3.UnitX, orientation);
        }

        public Vector3 GetUp()
        {
            //  TODO this is expensive
            var orientation = Quaternion.CreateFromYawPitchRoll(Rotation.Y * MathS.DegreesToRadians, Rotation.X * MathS.DegreesToRadians, Rotation.Z * MathS.DegreesToRadians);
            return Vector3.Transform(Vector3.UnitY, orientation);
        }

        public void Translate(Vector3 translation)
        {
            Position += translation;
            dirty = true;
        }

        public void Rotate(Vector3 rotation)
        {
            Rotation += rotation;
            dirty = true;
        }

        public void Scalar(Vector3 scale)
        {
            Scale *= scale;
            dirty = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Matrix4x4 ToMatrix4x4()
        {
            if (!dirty)
                return matrix4x4;

            float cosY = MathS.Cos(Rotation.Y * MathS.DegreesToRadians);
            float sinY = MathS.Sin(Rotation.Y * MathS.DegreesToRadians);

            float cosP = MathS.Cos(Rotation.X * MathS.DegreesToRadians);
            float sinP = MathS.Sin(Rotation.X * MathS.DegreesToRadians);

            float cosR = MathS.Cos(Rotation.Z * MathS.DegreesToRadians);
            float sinR = MathS.Sin(Rotation.Z * MathS.DegreesToRadians);

            var matrix = Matrix4x4.Identity;

            float sinRcosY = sinR * cosY;
            float sinYsinP = sinY * sinP;

            matrix.M11 = (cosY * cosR + sinYsinP * sinR) * Scale.X;
            matrix.M21 = cosR * sinYsinP - sinRcosY;
            matrix.M31 = cosP * sinY;

            matrix.M12 = cosP * sinR;
            matrix.M22 = cosR * cosP * Scale.Y;
            matrix.M32 = -sinP;

            matrix.M13 = sinRcosY * sinP - sinY * cosR;
            matrix.M23 = sinY * sinR + cosR * cosY * sinP;
            matrix.M33 = cosP * cosY * Scale.Z;

            matrix.M41 = Position.X;
            matrix.M42 = Position.Y;
            matrix.M43 = Position.Z;
            matrix.M44 = 1.0f;

            dirty = false;
            return matrix4x4 = matrix;
        }
    }
}

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
        public Vector3 Position { get; set; }

        public Vector3 Rotation { get; set; }

        public Vector3 Scale { get; set; }

        public void Translate(Vector3 translation)
        {
            Position += translation;
        }

        public void Rotate(Vector3 rotation)
        {
            Rotation += rotation;
        }

        public void Scalar(Vector3 scale)
        {
            Scale *= scale;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Matrix4x4 ToMatrix4x4()
        {
            float cosY = MathS.Cos(Rotation.Y);
            float sinY = MathS.Sin(Rotation.Y);

            float cosP = MathS.Cos(Rotation.X);
            float sinP = MathS.Sin(Rotation.X);

            float cosR = MathS.Cos(Rotation.Z);
            float sinR = MathS.Sin(Rotation.Z);

            var matrix = Matrix4x4.Identity;

            float sinRcosY = sinR * cosY;
            float sinYsinP = sinY * sinP;

            matrix.M11 = cosY * cosR + sinYsinP * sinR;
            matrix.M21 = cosR * sinYsinP - sinRcosY;
            matrix.M31 = cosP * sinY;

            matrix.M12 = cosP * sinR;
            matrix.M22 = cosR * cosP;
            matrix.M32 = -sinP;

            matrix.M13 = sinRcosY * sinP - sinY * cosR;
            matrix.M23 = sinY * sinR + cosR * cosY * sinP;
            matrix.M33 = cosP * cosY;

            matrix.M41 = Position.X;
            matrix.M42 = Position.Y;
            matrix.M43 = Position.Z;

            return matrix;
        }
    }
}

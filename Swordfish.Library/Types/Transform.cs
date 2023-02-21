using System.Numerics;

namespace Swordfish.Library.Types
{
    public struct Transform
    {
        public Vector3 Position { get; set; }

        public Vector3 Rotation { get; set; }

        public Vector3 Scale { get; set; }

        public Matrix4x4 ToMatrix4x4()
        {
            var rotationMatrix = Matrix4x4.CreateFromYawPitchRoll(Rotation.Y, Rotation.X, Rotation.Z);
            var positionMatrix = Matrix4x4.CreateTranslation(Position);
            return rotationMatrix * positionMatrix;
        }
    }
}

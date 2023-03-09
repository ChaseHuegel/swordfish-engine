using System.Numerics;

namespace Swordfish.Library.Types
{
    public struct Transform
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

        public Matrix4x4 ToMatrix4x4()
        {
            var rotationMatrix = Matrix4x4.CreateFromYawPitchRoll(Rotation.Y, Rotation.X, Rotation.Z);
            var positionMatrix = Matrix4x4.CreateTranslation(Position);
            return rotationMatrix * positionMatrix;
        }
    }
}

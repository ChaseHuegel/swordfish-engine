using OpenTK.Mathematics;

namespace Swordfish.ECS
{
    [Component]
    public struct RotationComponent
    {
        public Quaternion orientation;
        public Vector3 forward;
        public Vector3 right;
        public Vector3 up;

        public RotationComponent Rotate(Vector3 axis, float angle)
        {
            orientation = Quaternion.FromAxisAngle(orientation * axis, MathHelper.DegreesToRadians(-angle)) * orientation;

            forward = Vector3.Transform(-Vector3.UnitZ, orientation);
            right = Vector3.Transform(-Vector3.UnitX, orientation);
            up = Vector3.Transform(Vector3.UnitY, orientation);

            return this;
        }
    }
}

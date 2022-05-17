using OpenTK.Mathematics;

namespace Swordfish.Engine.ECS
{
    [Component]
    public struct TransformComponent
    {
        private int _parent;
        public int parent {
            get => _parent;

            set {
                //  Don't do anything if the parent isn't changing
                if (_parent == value)
                    return;

                //  If we are parented, local is offset by the origin
                if (_parent != Entity.Null)
                {
                    localPosition += Swordfish.ECS.Get<TransformComponent>(_parent).position;
                    localOrientation += Swordfish.ECS.Get<TransformComponent>(_parent).orientation;
                }

                _parent = value;
            }
        }


        public void Translate(Vector3 vec) { localPosition += vec; }
        public void Translate(float x, float y, float z) { localPosition.X += x; localPosition.Y += y; localPosition.Z += z; }

        public Vector3 localPosition;
        public Vector3 position {
            get {
                if (parent == Entity.Null)
                    return localPosition;
                else
                    return localPosition + Swordfish.ECS.Get<TransformComponent>(parent).position;
            }

            set {
                if (parent == Entity.Null)
                    localPosition = value;
                else
                    localPosition = value - Swordfish.ECS.Get<TransformComponent>(parent).position;
            }
        }


        public void Rotate(Vector3 axis, float angle)
        {
            orientation = Quaternion.FromAxisAngle(orientation * axis, MathHelper.DegreesToRadians(-angle)) * orientation;

            forward = Vector3.Transform(-Vector3.UnitZ, orientation);
            right = Vector3.Transform(-Vector3.UnitX, orientation);
            up = Vector3.Transform(Vector3.UnitY, orientation);
        }

        public Vector3 forward;
        public Vector3 right;
        public Vector3 up;

        private Quaternion _orientation;
        public Quaternion localOrientation {
            get => _orientation;

            set {
                _orientation = value;

                forward = Vector3.Transform(-Vector3.UnitZ, _orientation);
                right = Vector3.Transform(-Vector3.UnitX, _orientation);
                up = Vector3.Transform(Vector3.UnitY, _orientation);
            }
        }

        public Quaternion orientation {
            get {
                if (parent == Entity.Null)
                    return localOrientation;
                else
                    return localOrientation + Swordfish.ECS.Get<TransformComponent>(parent).orientation;
            }

            set {
                if (parent == Entity.Null)
                    localOrientation = value;
                else
                    localOrientation = value - Swordfish.ECS.Get<TransformComponent>(parent).orientation;
            }
        }
    }
}

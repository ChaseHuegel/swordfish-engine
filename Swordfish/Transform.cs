using OpenTK.Mathematics;

namespace Swordfish
{
    public class Transform
    {
        public Transform parent;

        public Vector3 localPosition;
        public Vector3 position {
            get
            {
                if (parent == null)
                    return localPosition;
                else
                    return localPosition + parent.position;
            }

            set
            {
                if (parent == null)
                {
                    localPosition = value;
                }
                else
                {
                    localPosition = value - parent.position;
                }
            }
        }

        public Quaternion orientation;
        public Vector3 rotation;
        private Vector3 lastRotation;

        private Vector3 _forward = new Vector3(0f, 0f, -1f);
        public Vector3 forward {
            get
            {
                TryUpdateDirections();
                return _forward;
            }
        }

        private Vector3 _up = new Vector3(0f, 1f, 0f);
        public Vector3 up {
            get
            {
                TryUpdateDirections();
                return _up;
            }
        }

        private Vector3 _right = new Vector3(-1f, 0f, 0f);
        public Vector3 right {
            get
            {
                TryUpdateDirections();
                return _right;
            }
        }

        private void TryUpdateDirections()
        {
            //  Only update directions if rotation has changed
            // if (rotation != lastRotation)
                UpdateDirections();

            lastRotation = rotation;
        }

        // private float cosX, cosY, sinX, sinY;
        private void UpdateDirections()
        {
            // cosY = (float)Math.Cos(MathHelper.DegreesToRadians(rotation.Y - 90));
            // cosX = (float)Math.Cos(MathHelper.DegreesToRadians(rotation.X));
            // sinX = (float)Math.Sin(MathHelper.DegreesToRadians(rotation.X));
            // sinY = (float)Math.Sin(MathHelper.DegreesToRadians(rotation.Y - 90));

            // _forward.X = cosY * cosX;
            // _forward.Y = sinX;
            // _forward.Z = sinY * cosX;
            // _forward = Quaternion.FromAxisAngle(Vector3.UnitZ, -rotation.Z) * _forward;
            // _forward.Normalize();

            // _right = Vector3.Cross(Quaternion.FromAxisAngle(Vector3.UnitZ, -rotation.Z) * Vector3.UnitY, _forward).Normalized();
            // _up = Vector3.Cross(_forward, _right).Normalized();

            _forward = Vector3.Transform(-Vector3.UnitZ, orientation).Normalized();
            _right = Vector3.Transform(-Vector3.UnitX, orientation).Normalized();
            _up = Vector3.Transform(Vector3.UnitY, orientation).Normalized();
        }

        public Transform Translate(Vector3 vector)
        {
            position += vector;
            return this;
        }

        public Transform Rotate(Vector3 axis, float angle)
        {
            orientation = Quaternion.FromAxisAngle(axis, MathHelper.DegreesToRadians(angle)) * orientation;
            UpdateDirections();

            return this;
        }

        public Matrix4 GetInverseMatrix()
        {
            return Matrix4.CreateTranslation(position * -1) * Matrix4.CreateFromQuaternion(orientation);
        }

        public Matrix4 GetMatrix()
        {
            return Matrix4.CreateFromQuaternion(orientation) * Matrix4.CreateTranslation(position);
        }

        public Transform(Transform parent = null)
        {
            this.parent = parent;
            this.position = new Vector3(0f, 0f, 0f);
            this.rotation = new Vector3(0f, 0f, 0f);
            this.orientation = Quaternion.Identity;

            UpdateDirections();
        }

        public Transform(Vector3 position, Vector3 rotation, Transform parent = null)
        {
            this.parent = parent;
            this.position = position;
            this.rotation = rotation;
            this.orientation = Quaternion.FromEulerAngles(rotation);

            UpdateDirections();
        }
    }
}
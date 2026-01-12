using System.Numerics;
using System.Runtime.CompilerServices;
using Swordfish.Library.Util;
// ReSharper disable UnusedMember.Global

namespace Swordfish.Library.Types;

//  TODO consider removing this
public class Transform
{
    private Vector3 Position
    {
        get
        {
            lock (_lock)
            {
                return _position;
            }
        }
        set
        {
            lock (_lock)
            {
                _dirty = true;
                _position = value;
            }
        }
    }

    private Quaternion Orientation
    {
        get
        {
            lock (_lock)
            {
                return _orientation;
            }
        }
        set
        {
            lock (_lock)
            {
                _dirty = true;
                _orientation = value;
            }
        }
    }

    private Vector3 Scale
    {
        get
        {
            lock (_lock)
            {
                return _scale;
            }
        }
        set
        {
            lock (_lock)
            {
                _dirty = true;
                _scale = value;
            }
        }
    }

    private readonly object _lock = new();
    private bool _dirty = true;
    private Vector3 _position;
    private Quaternion _orientation = Quaternion.Identity;
    private Vector3 _scale = Vector3.One;
    private Matrix4x4 _matrix4X4;

    public void Update(Vector3 position, Quaternion? orientation = null, Vector3? scale = null)
    {
        lock (_lock)
        {
            Position = position;
            
            if (orientation != null)
            {
                Orientation = orientation.Value;
            }
            
            if (scale != null)
            {
                Scale = scale.Value;
            }
        }
    }

    public Vector3 GetForward()
    {
        lock (_lock)
        {
            return Vector3.Transform(Vector3.UnitZ, _orientation);
        }
    }

    public Vector3 GetRight()
    {
        lock (_lock)
        {
            return Vector3.Transform(Vector3.UnitX, _orientation);
        }
    }

    public Vector3 GetUp()
    {
        lock (_lock)
        {
            return Vector3.Transform(Vector3.UnitY, _orientation);
        }
    }

    public void Translate(Vector3 translation)
    {
        lock (_lock)
        {
            Position += translation;
        }
    }

    public void Rotate(Vector3 rotation, bool local = false)
    {
        lock (_lock)
        {
            var eulerQuaternion = Quaternion.CreateFromYawPitchRoll(rotation.Y * MathS.DEGREES_TO_RADIANS,
                rotation.X * MathS.DEGREES_TO_RADIANS, rotation.Z * MathS.DEGREES_TO_RADIANS);
            if (local)
            {
                Orientation = Quaternion.Multiply(_orientation, eulerQuaternion);
            }
            else
            {
                Orientation = Quaternion.Multiply(eulerQuaternion, _orientation);
            }
        }
    }

    public void Scalar(Vector3 scale)
    {
        lock (_lock)
        {
            Scale *= scale;
        }
    }

    public (Vector3 Position, Quaternion Orientation, Vector3 Scale) Read()
    {
        lock (_lock)
        {
            return (Position, Orientation, Scale);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Matrix4x4 ToMatrix4X4()
    {
        lock (_lock)
        {
            if (!_dirty)
            {
                return _matrix4X4;
            }

            Vector3 forward = GetForward();
            Vector3 up = GetUp();
            Vector3 right = GetRight();

            var matrix = new Matrix4x4
            {
                M11 = right.X * _scale.X,
                M12 = right.Y * _scale.X,
                M13 = right.Z * _scale.X,
                M14 = 0,

                M21 = up.X * _scale.Y,
                M22 = up.Y * _scale.Y,
                M23 = up.Z * _scale.Y,
                M24 = 0,

                M31 = forward.X * _scale.Z,
                M32 = forward.Y * _scale.Z,
                M33 = forward.Z * _scale.Z,
                M34 = 0,

                M41 = _position.X,
                M42 = _position.Y,
                M43 = _position.Z,
                M44 = 1,
            };

            _matrix4X4 = matrix;
            _dirty = false;
            return matrix;
        }
    }
}
using System;
using System.Numerics;

namespace WaywardBeyond.Client.Core.Voxels.Models;

public struct Orientation : IEquatable<Orientation>
{
    public static readonly Orientation Identity = new(0,0,0);
    
    private static readonly Quaternion[] _precalculatedQuaternions = new Quaternion[64];
    private static readonly byte[] _lookupTable = new byte[36];
    
    /// <summary>
    ///     The number of 90° pitch rotations to apply, ranging 0 to 3.
    /// </summary>
    public int PitchRotations
    {
        get => _value & 3;
        set =>  _value = (byte)((_value & ~3) | (value & 3));
    }
    
    /// <summary>
    ///     The number of 90° yaw rotations to apply, ranging 0 to 3.
    /// </summary>
    public int YawRotations
    {
        get => (_value >> 2) & 3;
        set => _value = (byte)((_value & ~(3 << 2)) | ((value & 3) << 2));
    }
    
    /// <summary>
    ///     The number of 90° roll rotations to apply, ranging 0 to 3.
    /// </summary>
    public int RollRotations
    {
        get => (_value >> 4) & 3;
        set => _value = (byte)((_value & ~(3 << 4)) | ((value & 3) << 4));
    }
    
    private byte _value;
    
    static Orientation()
    {
        //  Init everything to 255 so it is possible to
        //  know which lookups have already been set
        Array.Fill(_lookupTable, (byte)255);
        
        for (var pitchRotations = 0; pitchRotations < 4; pitchRotations++)
        for (var yawRotations = 0; yawRotations < 4; yawRotations++)
        for (var rollRotations = 0; rollRotations < 4; rollRotations++)
        {
            var orientation = new Orientation(pitchRotations, yawRotations, rollRotations);
            
            float pitchRadians = pitchRotations * MathF.PI / 2f;
            float yawRadians = yawRotations * MathF.PI / 2f;
            float rollRadians = rollRotations * MathF.PI / 2f;

            var pitchQuaternion = Quaternion.CreateFromAxisAngle(Vector3.UnitX, pitchRadians);
            var yawQuaternion = Quaternion.CreateFromAxisAngle(Vector3.UnitY, yawRadians);
            var rollQuaternion = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, rollRadians);
            Quaternion quaternion = rollQuaternion * yawQuaternion * pitchQuaternion;

            _precalculatedQuaternions[orientation._value] = quaternion;

            Vector3 forward = Vector3.Transform(Vector3.UnitZ, quaternion);
            Vector3 up = Vector3.Transform(Vector3.UnitY, quaternion);

            int forwardAxisIndex = GetAxisIndex(forward);
            int upAxisIndex = GetAxisIndex(up);

            int tableIndex = forwardAxisIndex * 6 + upAxisIndex;
            
            //  Don't overwrite any already calculated orientations so "simple"
            //  orientations aren't overwritten by "complex" orientations
            if (_lookupTable[tableIndex] != 255)
            {
                continue;
            }

            _lookupTable[tableIndex] = orientation._value;
        }
    }
    
    /// <summary>
    ///     Creates an <see cref="Orientation"/> from a packed byte containing the number of 90° pitch, yaw, and roll rotations.
    /// </summary>
    public Orientation(byte value)
    {
        _value = (byte)(value & 0x3F);
    }

    /// <summary>
    ///     Creates an <see cref="Orientation"/> from a number of 90° pitch, yaw, and roll rotations.
    /// </summary>
    public Orientation(int pitch, int yaw, int roll)
    {
        _value = (byte)((pitch & 3) | ((yaw & 3) << 2) | ((roll & 3) << 4));
    }

    /// <summary>
    ///     Creates an <see cref="Orientation"/> from a quaternion.
    /// </summary>
    public Orientation(Quaternion quaternion)
    {
        Vector3 forward = Vector3.Transform(Vector3.UnitZ, quaternion);
        Vector3 up = Vector3.Transform(Vector3.UnitY, quaternion);

        int forwardAxisIndex = GetAxisIndex(forward);
        int upAxisIndex = GetAxisIndex(up);

        int tableIndex = forwardAxisIndex * 6 + upAxisIndex;
        byte cachedValue = _lookupTable[tableIndex];

        //  If somehow this isn't a valid orientation, fallback to identity
        if (cachedValue == 255)
        {
            cachedValue = 0;
        }

        _value = cachedValue;
    }
    
    /// <summary>
    ///     Pitches by a number of rotations.
    /// </summary>
    public void Pitch(int rotations = 1) => PitchRotations = (PitchRotations + rotations) & 3;
    
    /// <summary>
    ///     Yaws by a number of rotations.
    /// </summary>
    public void Yaw(int rotations = 1) => YawRotations = (YawRotations + rotations) & 3;
    
    /// <summary>
    ///     Rolls by a number of rotations.
    /// </summary>
    public void Roll(int rotations = 1) => RollRotations = (RollRotations + rotations) & 3;
    
    public static implicit operator byte(Orientation data) => data._value;
    public static implicit operator Orientation(byte data) => new(data);
    
    public override string ToString()
    {
        return $"<{PitchRotations * 90}, {YawRotations * 90}, {RollRotations * 90}>";
    }

    public readonly byte ToByte()
    {
        return _value;
    }
    
    public Quaternion ToQuaternion()
    {
        return _precalculatedQuaternions[_value];
    }
    
    public Vector3 ToEulerAngles()
    {
        return new Vector3(PitchRotations * 90, YawRotations * 90, RollRotations * 90);
    }

    public bool Equals(Orientation other)
    {
        return _value == other._value;
    }
    
    public override bool Equals(object? obj)
    {
        return obj is Orientation other && Equals(other);
    }
    
    public override int GetHashCode()
    {
        return _value.GetHashCode();
    }
    
    public static bool operator ==(Orientation left, Orientation right)
    {
        return left.Equals(right);
    }
    
    public static bool operator !=(Orientation left, Orientation right)
    {
        return !(left == right);
    }
    
    /// <summary>
    ///     Indexes a vector ranging 0-5 based on nearest axis.
    ///     Where 0: +X, 1: -X, 2: +Y, 3: -Y, 4: +Z, 5: -Z
    /// </summary>
    private static int GetAxisIndex(Vector3 vector)
    {
        float x = Math.Abs(vector.X);
        float y = Math.Abs(vector.Y);
        float z = Math.Abs(vector.Z);

        if (x > y && x > z)
        {
            return vector.X > 0 ? 0 : 1;
        }

        if (y > z)
        {
            return vector.Y > 0 ? 2 : 3;
        }

        return vector.Z > 0 ? 4 : 5;
    }
}

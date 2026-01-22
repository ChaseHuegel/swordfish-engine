using System;
using System.Numerics;

namespace WaywardBeyond.Client.Core.Voxels.Models;

using PrecalculatedQuaternion = (byte Value, Quaternion Quaternion);

public struct Orientation : IEquatable<Orientation>
{
    public static readonly Orientation Identity = new(0,0,0);
    
    private static readonly PrecalculatedQuaternion[] _precalculatedQuaternions;
    
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
        _precalculatedQuaternions = new PrecalculatedQuaternion[64];

        for (var pitchRotations = 0; pitchRotations < 4; pitchRotations++)
        for (var yawRotations = 0; yawRotations < 4; yawRotations++)
        for (var rollRotations = 0; rollRotations < 4; rollRotations++)
        {
            var orientation = new Orientation(pitchRotations, yawRotations, rollRotations);
            var precalculatedQuaternion = new PrecalculatedQuaternion(orientation.ToByte(), orientation.ToQuaternion());
            _precalculatedQuaternions[pitchRotations + 4 * (yawRotations + 4 * rollRotations)] = precalculatedQuaternion;
        }
    }
    
    /// <summary>
    ///     Creates an <see cref="Orientation"/> from a packed byte containing the number of 90° pitch, yaw, and roll rotations.
    /// </summary>
    public Orientation(byte value)
    {
        _value = value;
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
    public Orientation(Quaternion target)
    {
        PrecalculatedQuaternion bestMatch = _precalculatedQuaternions[0];
        
        float maxDot = -1f;
        for (var i = 0; i < _precalculatedQuaternions.Length; i++)
        {
            PrecalculatedQuaternion precalculatedQuaternion = _precalculatedQuaternions[i];
            
            float dot = Quaternion.Dot(target, precalculatedQuaternion.Quaternion);
            dot = Math.Abs(dot);

            if (!(dot > maxDot))
            {
                continue;
            }

            maxDot = dot;
            bestMatch = precalculatedQuaternion;
        }

        _value = bestMatch.Value;
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
        float pitchRadians = PitchRotations * MathF.PI / 2f;
        float yawRadians = YawRotations * MathF.PI / 2f;
        float rollRadians = RollRotations * MathF.PI / 2f;

        var qPitch = Quaternion.CreateFromAxisAngle(Vector3.UnitX, pitchRadians);
        var qYaw = Quaternion.CreateFromAxisAngle(Vector3.UnitY, yawRadians);
        var qRoll = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, rollRadians);

        return qRoll * qYaw * qPitch;
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
}

using System;
using System.Numerics;

namespace WaywardBeyond.Client.Core.Voxels.Models;

public struct Orientation : IEquatable<Orientation>
{
    public static readonly Orientation Identity = new(0,0,0);
    
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
    public Orientation(Quaternion quaternion)
    {
        float sinRCosP = 2.0f * (quaternion.W * quaternion.X + quaternion.Y * quaternion.Z);
        float cosRCosP = 1.0f - 2.0f * (quaternion.X * quaternion.X + quaternion.Y * quaternion.Y);
        float pitch = MathF.Atan2(sinRCosP, cosRCosP);
        pitch *= 180f / MathF.PI; 
        
        float sinP = 2.0f * (quaternion.W * quaternion.Y - quaternion.Z * quaternion.X);
        float yaw = MathF.Abs(sinP) >= 1 ? MathF.CopySign(MathF.PI / 2, sinP) : MathF.Asin(sinP);
        yaw *= 180f / MathF.PI;
        
        float sinYCosP = 2.0f * (quaternion.W * quaternion.Z + quaternion.X * quaternion.Y);
        float cosYCosP = 1.0f - 2.0f * (quaternion.Y * quaternion.Y + quaternion.Z * quaternion.Z);
        float roll = MathF.Atan2(sinYCosP, cosYCosP);
        roll *= 180f / MathF.PI;
        
        PitchRotations = (int)Math.Round(pitch / 90, MidpointRounding.ToEven);
        YawRotations = (int)Math.Round(yaw / 90, MidpointRounding.ToEven);
        RollRotations = (int)Math.Round(roll / 90, MidpointRounding.ToEven);
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
    
    public Matrix4x4 ToMatrix4x4()
    {
        float pitch = PitchRotations * MathF.PI / 2f;
        float yaw = YawRotations * MathF.PI / 2f;
        float roll = RollRotations * MathF.PI / 2f;

        Matrix4x4 rotation =
            Matrix4x4.CreateRotationX(pitch) *
            Matrix4x4.CreateRotationY(yaw) *
            Matrix4x4.CreateRotationZ(roll);

        return rotation;
    }
    
    public Quaternion ToQuaternion()
    {
        float pitch = PitchRotations * MathF.PI / 2f;
        float yaw = YawRotations * MathF.PI / 2f;
        float roll = RollRotations * MathF.PI / 2f;

        return Quaternion.CreateFromYawPitchRoll(yaw, pitch, roll);
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

using System.Numerics;

namespace Swordfish.Bricks;

public struct BrickOrientation
{
    public static readonly BrickOrientation Identity = new(0,0,0);
    
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
    ///     Creates a <see cref="BrickOrientation"/> from a packed byte containing the number of 90° pitch, yaw, and roll rotations.
    /// </summary>
    public BrickOrientation(byte value)
    {
        _value = value;
    }

    /// <summary>
    ///     Creates a <see cref="BrickOrientation"/> from a number of 90° pitch, yaw, and roll rotations.
    /// </summary>
    public BrickOrientation(int pitch, int yaw, int roll)
    {
        _value = (byte)((pitch & 3) | ((yaw & 3) << 2) | ((roll & 3) << 4));
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
    
    public override string ToString()
    {
        return $"<{PitchRotations * 90}, {YawRotations * 90}, {RollRotations * 90}>";
    }

    public byte ToByte()
    {
        return _value;
    }
    
    public Matrix4x4 ToMatrix4x4()
    {
        float pitch = PitchRotations * MathF.PI / 2f;
        float yaw = YawRotations   * MathF.PI / 2f;
        float roll = RollRotations  * MathF.PI / 2f;

        Matrix4x4 rotation =
            Matrix4x4.CreateRotationX(pitch) *
            Matrix4x4.CreateRotationY(yaw) *
            Matrix4x4.CreateRotationZ(roll);

        return rotation;
    }
    
    public Quaternion ToQuaternion()
    {
        float pitch = PitchRotations * MathF.PI / 2f;
        float yaw = YawRotations   * MathF.PI / 2f;
        float roll = RollRotations  * MathF.PI / 2f;

        return Quaternion.CreateFromYawPitchRoll(yaw, pitch, roll);
    }
    
    public Vector3 ToEulerAngles()
    {
        float pitch = PitchRotations * MathF.PI / 2f;
        float yaw = YawRotations   * MathF.PI / 2f;
        float roll = RollRotations  * MathF.PI / 2f;

        return new Vector3(pitch * 90, yaw * 90, roll * 90);
    }
}

namespace WaywardBeyond.Voxels;

/// <summary>Packs a 6-bit voxel <see cref="Orientation"/> and 2 per-voxel flag bits into one byte.</summary>
public readonly struct OrientationFlags
{
    /// <summary>Bit mask covering the 6-bit voxel orientation (bits 0..5).</summary>
    public const byte ORIENTATION_MASK = 0x3F;

    /// <summary>Bit 6: set when the voxel has at least one entry in its chunk's face override map.</summary>
    public const byte HAS_FACE_OVERRIDES_BIT = 0x40;

    private readonly byte _value;

    /// <summary>Unpacks an <see cref="OrientationFlags"/> value from a raw byte.</summary>
    public OrientationFlags(byte value)
    {
        _value = value;
    }

    /// <summary>Packs an <see cref="Orientation"/> and face-override flag into one byte.</summary>
    public OrientationFlags(Orientation orientation, bool hasFaceOverrides = false)
    {
        _value = (byte)((orientation.ToByte() & ORIENTATION_MASK) | (hasFaceOverrides ? HAS_FACE_OVERRIDES_BIT : 0));
    }

    /// <summary>The 6-bit brick orientation.</summary>
    public Orientation Orientation => new((byte)(_value & ORIENTATION_MASK));

    /// <summary>True when this voxel carries one or more per-face overrides in its chunk sidecar.</summary>
    public bool HasFaceOverrides => (_value & HAS_FACE_OVERRIDES_BIT) != 0;

    /// <summary>Returns a copy of this value with <see cref="HasFaceOverrides"/> set or cleared.</summary>
    public OrientationFlags WithFaceOverrides(bool hasFaceOverrides)
    {
        return new OrientationFlags(
            (byte)(hasFaceOverrides ? (_value | HAS_FACE_OVERRIDES_BIT) : (_value & ~HAS_FACE_OVERRIDES_BIT))
        );
    }

    /// <summary>Returns the raw packed byte.</summary>
    public byte ToByte() => _value;

    public static implicit operator byte(OrientationFlags data) => data._value;
    public static implicit operator OrientationFlags(byte data) => new(data);
}

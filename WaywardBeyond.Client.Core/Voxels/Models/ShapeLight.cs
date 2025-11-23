using WaywardBeyond.Client.Core.Bricks;

namespace WaywardBeyond.Client.Core.Voxels.Models;

public readonly struct ShapeLight
{
    private readonly byte _value;
    
    public ShapeLight(byte value)
    {
        _value = value;
    }
    
    public ShapeLight(BrickShape shape, int lightLevel)
    {
        int high = (byte)shape;
        int low = (byte)lightLevel << 4;
        _value = (byte)(high | low);
    }
    
    public BrickShape Shape => (BrickShape)(_value & 0x0F);
    
    public int LightLevel => (_value >> 4) & 0x0F;
    
    public static implicit operator byte(ShapeLight data) => data._value;
    public static implicit operator ShapeLight(byte data) => new(data);
}
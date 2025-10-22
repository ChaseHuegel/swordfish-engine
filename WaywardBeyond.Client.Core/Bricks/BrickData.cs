namespace WaywardBeyond.Client.Core.Bricks;

public readonly struct BrickData
{
    private readonly byte _value;
    
    public BrickData(byte value)
    {
        _value = value;
    }
    
    public BrickData(BrickShape shape, int lightLevel)
    {
        int high = (byte)shape;
        int low = (byte)lightLevel << 4;
        _value = (byte)(high | low);
    }
    
    public BrickShape Shape => (BrickShape)(_value & 0x0F);
    
    public int LightLevel => (_value >> 4) & 0x0F;
    
    public static implicit operator byte(BrickData data) => data._value;
    public static implicit operator BrickData(byte data) => new(data);
}
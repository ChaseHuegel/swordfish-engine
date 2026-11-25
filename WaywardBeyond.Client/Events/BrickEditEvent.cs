using WaywardBeyond.Bricks;

namespace WaywardBeyond.Client.Events;

/// <summary>Raised when a brick is placed or broken in the world for presentation listeners.</summary>
internal readonly struct BrickEditEvent(ushort brickID, BrickSoundEvent kind)
{
    public readonly ushort BrickID = brickID;
    public readonly BrickSoundEvent Kind = kind;
}

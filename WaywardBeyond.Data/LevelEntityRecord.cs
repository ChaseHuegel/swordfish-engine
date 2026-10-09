namespace WaywardBeyond.Data;

/// <summary>One persisted voxel entity row, keyed by its entity uuid.</summary>
public readonly struct LevelEntityRecord(in ulong uuid, in byte[] data)
{
    public readonly ulong Uuid = uuid;
    public readonly byte[] Data = data;
}

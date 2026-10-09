namespace WaywardBeyond.Bricks;

/// <summary>
/// Maps brick names to their 16-bit voxel ids for one loaded content set.
/// Id 0 (the empty voxel) is the fallback for an unknown name.
/// </summary>
public interface IBrickIdMap
{
    /// <summary>Returns the voxel id for a brick name, or 0 (empty) when not registered.</summary>
    ushort Id(string name);

    /// <summary>Returns the brick name for a voxel id, or null when not registered.</summary>
    string? Name(ushort id);

    /// <summary>Returns the number of registered bricks.</summary>
    int Count { get; }
}
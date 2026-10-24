namespace WaywardBeyond.Bricks;

/// <summary>A deterministic, collision-free mapping from brick names to 16-bit ids for one content load.</summary>
/// <remarks>
///     ID 0 (the empty brick) is the fallback for an unknown name.
///     Assigned ids are transient; the string name is the canonical identity.
/// </remarks>
public interface IBrickRegistry
{
    /// <summary>Returns the voxel id for a brick name, or 0 (empty) when not registered.</summary>
    ushort Id(string name);

    /// <summary>Returns the brick name for an id, or null when not registered.</summary>
    string? Name(ushort id);
}
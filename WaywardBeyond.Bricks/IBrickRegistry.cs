namespace WaywardBeyond.Bricks;

/// <summary>A deterministic, collision-free mapping from brick names to 16-bit ids for one content load.</summary>
/// <remarks>
///     ID 0 (the empty brick) is the fallback for an unknown name.
///     Assigned ids are transient; the string name is the canonical identity.
///     Registered bricks from loaded content occupy the public ID space (1 .. Count) queried by Id(name), whereas unknown brick names decoded from external data are allocated process-stable IDs in a separate extension space via Resolve(name), and Name(id) resolves both spaces.
/// </remarks>
public interface IBrickRegistry
{
    /// <summary>Returns the voxel id for a registered brick name, or 0 (empty) when not registered.</summary>
    ushort Id(string name);

    /// <summary>Returns the voxel id for a brick name, allocating a stable extension id when not registered, or 0 when empty.</summary>
    ushort Resolve(string name) => Id(name);

    /// <summary>Returns the brick name for a registered or allocated extension id, or null when unknown.</summary>
    string? Name(ushort id);
}
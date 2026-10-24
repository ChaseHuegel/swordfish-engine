using Swordfish.ECS;
using WaywardBeyond.Data;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Server.Components;

/// <summary>
/// Carries a structure's live, mutable <see cref="VoxelObject"/> on its authority entity so the server
/// can apply voxel edits (break/place) that the shared interaction resolver targets. Saved and streamed
/// <see cref="ChunkInfo"/>[] data is re-derived from this container, so it is the single voxel
/// representation on the authority side. Deliberately not a replication-set component - authoritative
/// edits are broadcast as deltas, not as this state.
/// </summary>
public readonly struct VoxelWorldComponent(in VoxelObject voxelObject) : IDataComponent
{
    /// <summary>The live, mutable voxel container the server owns.</summary>
    public readonly VoxelObject VoxelObject = voxelObject;
}
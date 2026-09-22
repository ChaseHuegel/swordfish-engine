using Swordfish.ECS;
using WaywardBeyond.Client.Core.Voxels;
using WaywardBeyond.Shared.Data;

namespace WaywardBeyond.Shared.Gameplay;

/// <summary>
/// Carries a structure's live, mutable <see cref="VoxelObject"/> on its authority entity so the server
/// can apply voxel edits (break/place) that the shared interaction resolver targets. The serialized
/// <see cref="VoxelEntityDataComponent.Chunks"/> are re-derived from this container after an edit so the
/// server's world save reflects the change. Deliberately not a replication-set component - authoritative
/// edits are broadcast as deltas, not as this state.
/// </summary>
public struct VoxelWorldComponent(in VoxelObject voxelObject) : IDataComponent
{
    public readonly VoxelObject VoxelObject = voxelObject;
}
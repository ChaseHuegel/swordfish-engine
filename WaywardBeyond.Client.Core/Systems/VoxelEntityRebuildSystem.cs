using Swordfish.ECS;
using WaywardBeyond.Client.Core.Components;
using WaywardBeyond.Client.Core.Voxels.Building;

namespace WaywardBeyond.Client.Core.Systems;

/// <summary>
/// Observes the client's dirty <see cref="VoxelComponent"/> flags and fulfills the mesh/collider rebuild
/// on the ECS thread. Producers (the interaction prediction and voxel reconcile paths) publish an edit by
/// mutating the shared <see cref="VoxelObject"/> and marking the component dirty; this system rebuilds the
/// affected entity's renderers and clears the flag. Rebuild must run after the producers in the ECS tick
/// order so the dirty flag it consumes reflects the latest edit.
/// </summary>
internal sealed class VoxelEntityRebuildSystem : IEntitySystem
{
    private readonly VoxelEntityBuilder _voxelBuilder;

    public VoxelEntityRebuildSystem(in VoxelEntityBuilder voxelBuilder)
    {
        _voxelBuilder = voxelBuilder;
    }

    private struct RebuildAction : IForEach<VoxelComponent>
    {
        public VoxelEntityRebuildSystem Owner;

        public void Execute(float delta, DataStore store, int entity, in VoxelComponent voxelComponent)
        {
            Owner._voxelBuilder.Rebuild(store, entity);
            store.ClearDirty<VoxelComponent>(entity);
        }
    }

    public void Tick(float delta, DataStore store)
    {
        RebuildAction action = new() { Owner = this };
        store.QueryDirty<VoxelComponent, RebuildAction>(0f, ref action);
    }
}
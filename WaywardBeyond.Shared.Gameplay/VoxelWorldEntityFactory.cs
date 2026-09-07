using System.Numerics;
using Swordfish.ECS;
using WaywardBeyond.Shared.Data;
using WaywardBeyond.Shared.Networking.Components;

namespace WaywardBeyond.Shared.Gameplay;

/// <summary>
/// Server-authority construction of a voxel world entity from its serialized <see cref="VoxelEntityData"/>.
/// Builds exactly the components the server needs to own and replicate a dynamic structure body - transform,
/// physics, collision collider (derived from voxel occupancy via <see cref="VoxelColliderBuilder"/>) and a
/// <see cref="NetworkComponent"/> so the authoritative <c>TransformComponent</c>/<c>PhysicsComponent</c>
/// flow downstream through the normal snapshot path. It deliberately omits render/mesh/content components
/// (meshes, per-voxel children, decorators, <c>VoxelComponent</c>) - those are view-only. The entity uuid
/// comes from the data so it matches the client's view copy, which is what lets reconcile correct drift.
/// </summary>
public static class VoxelWorldEntityFactory
{
    public static Entity CreateAuthority(in DataStore store, in VoxelEntityData data)
    {
        //  Skip entities with no solid voxels - an empty CompoundShape would otherwise be handed to the
        //  physics system, which is a degenerate/undefined body. Such entities never occur in practice
        //  (chunks are only emitted once a voxel is set), so this is a defensive guard.
        if (!HasSolidVoxels(data))
        {
            return default;
        }

        int entity = store.Alloc(Uuid.FromValue(data.Uuid));

        store.AddOrUpdate(entity, new IdentifierComponent(name: null, tag: "game"));
        store.AddOrUpdate(entity, new TransformComponent(
            new Vector3((float)data.X, (float)data.Y, (float)data.Z),
            new Quaternion(data.OrientationX, data.OrientationY, data.OrientationZ, data.OrientationW),
            new Vector3(data.ScaleX, data.ScaleY, data.ScaleZ)
        ));
        store.AddOrUpdate(entity, PlayerBodyConfig.CreatePhysics());
        store.AddOrUpdate(entity, new ColliderComponent(VoxelColliderBuilder.BuildCollition(data.Chunks)));
        store.AddOrUpdate(entity, new VoxelEntityDataComponent(data.Chunks));
        store.AddOrUpdate(entity, new NetworkComponent());

        return new Entity(entity, store);
    }

    private static bool HasSolidVoxels(in VoxelEntityData data)
    {
        ChunkInfo[] chunks = data.Chunks;
        if (chunks == null || chunks.Length == 0)
        {
            return false;
        }

        for (var c = 0; c < chunks.Length; c++)
        {
            Voxel[] voxels = chunks[c].Chunk.Voxels;
            if (voxels == null)
            {
                continue;
            }

            for (var i = 0; i < voxels.Length; i++)
            {
                if (voxels[i].ID != 0)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
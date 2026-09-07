using System.Numerics;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.ECS;
using Swordfish.Physics.Jolt;
using Swordfish.Settings;
using WaywardBeyond.Shared.Data;
using WaywardBeyond.Shared.Gameplay;
using WaywardBeyond.Shared.Networking.Components;
using Xunit;

namespace Swordfish.Tests;

/// <summary>
/// Phase 2 (2.1) acceptance: the server authority world builds its own voxel structures and their Jolt
/// colliders so movement resolves against them - no longer arena-only. The shared
/// <see cref="VoxelWorldEntityFactory"/> emits exactly the components the server needs (transform, physics,
/// collider, replication marker) with no render/content components.
/// </summary>
public class ServerVoxelWorldTests
{
    [Fact]
    public void AuthorityFactoryBuildsNetworkedCollidableStructure()
    {
        var store = new DataStore();
        VoxelEntityData data = CreateSolidCube(0, 0, 0);

        Entity entity = VoxelWorldEntityFactory.CreateAuthority(store, data);
        int entityPtr = entity.Ptr;

        Assert.True(store.TryGet(entityPtr, out TransformComponent transform));
        Assert.Equal(new Vector3((float)data.X, (float)data.Y, (float)data.Z), transform.Position);
        Assert.True(store.TryGet(entityPtr, out PhysicsComponent physics));
        Assert.True(store.TryGet(entityPtr, out ColliderComponent collider));
        Assert.True(collider.CompoundShape.HasValue);
        Assert.NotEmpty(collider.CompoundShape.Value.Shapes);
        Assert.True(store.TryGet(entityPtr, out NetworkComponent net));
    }

    [Fact]
    public void ServerPhysicsStopsMovementAgainstVoxelStructure()
    {
        var store = new DataStore();

        //  A solid voxel structure spanning [-0.5, 0.5]^3 at the origin.
        VoxelWorldEntityFactory.CreateAuthority(store, CreateSolidCube(0, 0, 0));

        //  A small dynamic body (the player shape) fired at it head-on. Without a server collider this
        //  would fly straight through the origin; with one it must be stopped short of the far face.
        int projectile = store.Alloc();
        store.AddOrUpdate(projectile, new TransformComponent(new Vector3(0f, 0f, -3f)));
        PhysicsComponent projectilePhysics = PlayerBodyConfig.CreatePhysics();
        projectilePhysics.Velocity = new Vector3(0f, 0f, 12f);
        store.AddOrUpdate(projectile, projectilePhysics);
        store.AddOrUpdate(projectile, PlayerBodyConfig.CreateCollider(PlayerBodyConfig.PLAYER_SCALE));

        var physics = new JoltPhysicsSystem(NullLogger<JoltPhysicsSystem>.Instance, new PhysicsSettings());
        physics.SetGravity(Vector3.Zero);

        //  Enough steps to cross ~3 units at 12u/s (0.25s) and then some, so an unblocked body ends deep
        //  on the far side instead of having only just reached the structure.
        for (var i = 0; i < 600; i++)
        {
            physics.Tick(0.016f, store);
        }

        Assert.True(store.TryGet(projectile, out TransformComponent transform));
        Assert.True(transform.Position.Z < 0.75f,
            $"The projectile should be stopped at the structure's near face, not pass through it (z={transform.Position.Z}).");
    }

    [Fact]
    public void StructureTransformPlacesCollisionInWorld()
    {
        var store = new DataStore();
        VoxelWorldEntityFactory.CreateAuthority(store, CreateSolidCube(5f, 0f, 0f));

        //  A projectile fired along +X toward the structure offset at x=5.
        int projectile = store.Alloc();
        store.AddOrUpdate(projectile, new TransformComponent(new Vector3(0f, 0f, 0f)));
        PhysicsComponent projectilePhysics = PlayerBodyConfig.CreatePhysics();
        projectilePhysics.Velocity = new Vector3(12f, 0f, 0f);
        store.AddOrUpdate(projectile, projectilePhysics);
        store.AddOrUpdate(projectile, PlayerBodyConfig.CreateCollider(PlayerBodyConfig.PLAYER_SCALE));

        var physics = new JoltPhysicsSystem(NullLogger<JoltPhysicsSystem>.Instance, new PhysicsSettings());
        physics.SetGravity(Vector3.Zero);
        for (var i = 0; i < 600; i++)
        {
            physics.Tick(0.016f, store);
        }

        Assert.True(store.TryGet(projectile, out TransformComponent transform));
        Assert.True(transform.Position.X < 5.75f,
            $"A structure offset at x=5 should block the projectile short of its far face (x={transform.Position.X}).");
    }

    private static VoxelEntityData CreateSolidCube(float x, float y, float z)
    {
        const byte chunkSize = 4;
        var voxels = new Voxel[chunkSize * chunkSize * chunkSize];
        voxels[0] = new Voxel(1, 0, 0);

        var chunk = new Chunk(chunkSize, voxels);
        var chunkInfo = new ChunkInfo(0, 0, 0, chunk);

        return new VoxelEntityData(
            0xBEEF,
            x, y, z,
            0f, 0f, 0f, 1f,
            1f, 1f, 1f,
            [chunkInfo]
        );
    }
}
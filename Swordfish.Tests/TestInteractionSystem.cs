using System;
using System.Numerics;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.ECS;
using Swordfish.Physics;
using WaywardBeyond.Client.Core.Bricks;
using WaywardBeyond.Client.Core.Voxels;
using WaywardBeyond.Shared.Data;
using WaywardBeyond.Shared.Gameplay;
using WaywardBeyond.Shared.Networking.Transport;
using WaywardBeyond.Server.Core.Systems;

namespace Swordfish.Tests;

/// <summary>
/// Creates a headless <see cref="ServerInteractionSystem"/> for tests that construct a
/// <see cref="ServerJoinSystem"/> but never exercise the interaction path (the join system only needs
/// it for per-entity sequence-watermark resets). The physics/content stubs are inert.
/// </summary>
internal static class TestInteractionSystem
{
    public static ServerInteractionSystem Create(ServerConnectionHub hub)
    {
        return new ServerInteractionSystem(
            hub,
            new StubPhysics(),
            new StubContent(),
            NullLogger<ServerInteractionSystem>.Instance,
            _ => new StubWorld()
        );
    }

    private sealed class StubWorld : IVoxelInteractionWorld
    {
        public bool TryRaycast(in Ray ray, out RaycastResult result)
        {
            result = default;
            return false;
        }

        public bool TryGetVoxelTarget(int entity, out VoxelObject? voxelObject, out TransformComponent transform)
        {
            voxelObject = null;
            transform = default;
            return false;
        }

        public bool TryGetVoxelTarget(in Uuid entityUuid, out int entity, out VoxelObject? voxelObject, out TransformComponent transform)
        {
            entity = default;
            voxelObject = null;
            transform = default;
            return false;
        }
    }

    private sealed class StubPhysics : IPhysics
    {
        public event EventHandler<EventArgs>? FixedUpdate;
        public RaycastResult Raycast(in Ray ray) => default;
        public void SetGravity(Vector3 gravity) { }
    }

    private sealed class StubContent : IInteractionContent
    {
        public bool TryGetPlaceable(string? itemID, out PlaceableBrick placeable)
        {
            placeable = new PlaceableBrick(0, BrickShape.Block, shapeable: false, hasOrientableTag: false, brightness: 0);
            return false;
        }

        public bool TryGetLoot(ushort brickDataID, out ItemData loot)
        {
            loot = default;
            return false;
        }
    }
}
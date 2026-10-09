using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.ECS;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Voxels;
using WaywardBeyond.Data;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Networking.Transport;
using WaywardBeyond.Server.Systems;

namespace Swordfish.Tests;

/// <summary>
/// Creates a headless <see cref="ServerInteractionSystem"/> for tests that construct a
/// <see cref="ServerJoinSystem"/> but never exercise the interaction path (the join system only needs
/// it for per-entity sequence-watermark resets). The content stub is inert.
/// </summary>
internal static class TestInteractionSystem
{
    public static ServerInteractionSystem Create(ServerConnectionHub hub)
    {
        return new ServerInteractionSystem(
            hub,
            new StubContent(),
            NullLogger<ServerInteractionSystem>.Instance,
            _ => new StubWorld(),
            TestBricks.Map
        );
    }

    private sealed class StubWorld : IVoxelInteractionWorld
    {
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

    private sealed class StubContent : IInteractionContent
    {
        public bool TryGetPlaceable(string? itemID, out PlaceableBrick placeable)
        {
            placeable = new PlaceableBrick("wb:panel", BrickShape.Block, shapeable: false, hasOrientableTag: false, brightness: 0);
            return false;
        }

        public bool TryGetLoot(ushort brickDataID, out ItemData loot)
        {
            loot = default;
            return false;
        }
    }
}
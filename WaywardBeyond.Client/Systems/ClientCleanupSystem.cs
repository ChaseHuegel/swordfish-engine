using System.Collections.Concurrent;
using Krill;
using Microsoft.Extensions.Logging;
using Swordfish.ECS;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Client.Voxels.Building;

namespace WaywardBeyond.Client.Systems;

/// <summary>
/// Tears down the client world when the player returns to the menu. The teardown must run on the ECS
/// thread (its Tick), never the UI thread: it frees every entity tagged "game". The tag covers the
/// local player, the remote player mirrors, and the world geometry. Component resources (renderers,
/// meshes, batches, and physics bodies) are released by the engine's removed-component processors on
/// the following tick, after <see cref="DataStore.Free"/> has captured them. <see cref="ClientReconcileSystem"/>
/// is already gated off by the menu state, so nothing re-allocates entities during this clean.
/// </summary>
internal sealed class ClientCleanupSystem : IEntitySystem
{
    private readonly ILogger<ClientCleanupSystem> _logger;
    private readonly ConcurrentQueue<byte> _requests = new();

    public ClientCleanupSystem(in ILogger<ClientCleanupSystem> logger)
    {
        _logger = logger;
    }

    /// <summary>Queues a full world teardown to run on the ECS thread.</summary>
    public void RequestCleanup()
    {
        _requests.Enqueue(0);
    }

    public void Tick(float delta, DataStore store)
    {
        if (!_requests.TryDequeue(out _))
        {
            return;
        }

        FreeGameAction game = new();
        store.Query<IdentifierComponent, FreeGameAction>(0f, ref game);

        _logger.LogInformation("Cleaned up the client world on the ECS thread.");
    }

    private struct FreeGameAction : IForEach<IdentifierComponent>
    {
        public void Execute(float delta, DataStore store, int entity, in IdentifierComponent identifier)
        {
            if (identifier.Tag != "game")
            {
                return;
            }

            if (store.TryGet(entity, out VoxelComponent _))
            {
                VoxelEntityBuilder.FreeChunks(store, entity);
            }

            store.Free(entity);
        }
    }
}

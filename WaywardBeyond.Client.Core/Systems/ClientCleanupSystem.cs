using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Swordfish.ECS;
using Swordfish.Graphics;
using WaywardBeyond.Client.Core.Components;

namespace WaywardBeyond.Client.Core.Systems;

/// <summary>
/// Tears down the client world when the player returns to the menu. The teardown must run on the ECS
/// thread (its Tick), never the UI thread: it frees every game entity and the player while disposing
/// their renderers and physics bodies. Disposing bodies posts their native destruction to the physics
/// <see cref="System.Threading.ThreadContext"/> on the same thread, and freeing entities here avoids
/// racing the ECS physics/render systems - a UI-thread teardown that raced the solver corrupted Jolt and
/// crashed on rejoin. <see cref="ClientReconcileSystem"/> is already gated off by the menu state, so
/// nothing re-allocates entities during this clean.
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

        FreePlayerAction player = new();
        store.Query<PlayerComponent, FreePlayerAction>(0f, ref player);

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

            if (store.TryGet(entity, out MeshRendererComponent mesh) && mesh.MeshRenderer != null)
            {
                mesh.MeshRenderer.Dispose();
                mesh.MeshRenderer.Mesh.Dispose();
            }

            if (store.TryGet(entity, out PhysicsComponent physics))
            {
                physics.Dispose();
            }

            store.Free(entity);
        }
    }

    private struct FreePlayerAction : IForEach<PlayerComponent>
    {
        public void Execute(float delta, DataStore store, int entity, in PlayerComponent player)
        {
            if (store.TryGet(entity, out PhysicsComponent physics))
            {
                physics.Dispose();
            }

            store.Free(entity);
        }
    }
}
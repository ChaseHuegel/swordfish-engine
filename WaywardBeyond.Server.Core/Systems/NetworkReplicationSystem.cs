using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Swordfish.ECS;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Components;
using WaywardBeyond.Shared.Networking.Registry;
using WaywardBeyond.Shared.Networking.Transport;

namespace WaywardBeyond.Server.Core.Systems;

/// <summary>
/// Server-side replication, split into an ordered <see cref="ApplyStage"/> and <see cref="PublishStage"/>
/// so the server can consume physics + the shared motion step between them. <see cref="ApplyStage"/>
/// drains inbound client-owned components across every connected client, staging each
/// <see cref="InputComponent"/> in the server entity's sim-tick-keyed command buffer (the snapshot's
/// entity uuid addresses the target, so cross-connection routing is unnecessary). <see cref="PublishStage"/>
/// publishes authoritative server-owned snapshots plus despawns to each client, composing a per-client
/// <see cref="WorldSnapshot"/> whose <see cref="WorldSnapshot.LastProcessedInput"/> reflects that client
///'s own acked input. Snapshot <see cref="WorldSnapshot.TickNumber"/> is the server's current sim tick
/// (physics-step ordinal), set via <see cref="SimTick"/>.
/// </summary>
public sealed class NetworkReplicationSystem : IEntitySystem
{
    private readonly ServerConnectionHub _hub;
    private readonly SessionManager _sessions;
    private readonly ILogger<NetworkReplicationSystem> _logger;

    private readonly List<ComponentSnapshot> _pending = [];
    private readonly Queue<ulong> _removed = [];

    //  Clients awaiting a one-shot full-state snapshot (full server-owned state of every networked
    //  entity) on their next publish, in place of that tick's delta. Newly joined clients request it so
    //  pre-existing players - whose dirty markings were already published-and-cleared before the client
    //  connected - still materialize as remote players.
    private readonly HashSet<Uuid> _fullSync = [];

    public uint SimTick { get; set; }

    public NetworkReplicationSystem(
        in ServerConnectionHub hub,
        SessionManager sessions,
        in ILogger<NetworkReplicationSystem> logger
    ) {
        _hub = hub;
        _sessions = sessions;
        _logger = logger;
    }

    /// <summary>
    /// Records an entity uuid to publish as despawned to connected clients. Must be called before the
    /// entity is freed: <c>DataStore.Free</c> clears the entity's uuid, so the despawn uuid can only be
    /// captured at the moment ownership is released.
    /// </summary>
    public void RequestDespawn(ulong entityUuid)
    {
        _removed.Enqueue(entityUuid);
    }

    /// <summary>
    ///     Records a client to receive a one-shot full-state snapshot (every server-owned component of
    ///     every currently networked entity) on the next publish, in place of that tick's delta. The full
    ///     state is delivered regardless of dirty markings, which the delta stream would otherwise have
    ///     consumed before a late-joining client connected.
    /// </summary>
    public void RequestFullSync(Uuid clientId)
    {
        _fullSync.Add(clientId);
    }

    public void Tick(float delta, DataStore store)
    {
        ApplyStage(delta, store);
        PublishStage(delta, store);
    }

    /// <summary>Drains and applies inbound client-owned components from all connected clients.</summary>
    public void ApplyStage(float delta, DataStore store)
    {
        foreach ((_, WorldSnapshot snapshot) in _hub.Receive<WorldSnapshot>())
        {
            ComponentSnapshot[] components = snapshot.Components;
            for (var i = 0; i < components.Length; i++)
            {
                ApplyComponent(store, components[i]);
            }
        }
    }

    /// <summary>
    /// Collects authoritative server-owned snapshots once, then publishes to each client a per-client
    /// snapshot carrying that client's own <see cref="WorldSnapshot.LastProcessedInput"/>. Despawns are
    /// those queued via <see cref="RequestDespawn"/>.
    /// </summary>
    public void PublishStage(float delta, DataStore store)
    {
        _pending.Clear();

        OnTickAction onTick = new() { Owner = this };
        store.Query<NetworkComponent, OnTickAction>(delta, ref onTick);

        if (_pending.Count == 0 && _removed.Count == 0 && _fullSync.Count == 0)
        {
            return;
        }

        ComponentSnapshot[] components = _pending.ToArray();
        ulong[] removed = _removed.ToArray();
        _removed.Clear();

        ComponentSnapshot[]? fullState = null;
        if (_fullSync.Count > 0)
        {
            //  Read-only snapshot of every server-owned component on every networked entity, regardless
            //  of dirty, for clients that joined after those components were last published.
            CollectFullStateAction collect = new();
            store.Query<NetworkComponent, CollectFullStateAction>(delta, ref collect);
            fullState = collect.Components.ToArray();
        }

        foreach ((Uuid clientId, _) in _hub.Clients)
        {
            uint lastProcessedInput = 0;
            if (_sessions.TryGetEntity(clientId, out int entity)
                && store.TryGet(entity, out NetworkComponent net))
            {
                lastProcessedInput = net.LastAckedInput;
            }

            if (_fullSync.Remove(clientId))
            {
                _hub.Send(clientId, new WorldSnapshot
                {
                    TickNumber = SimTick,
                    LastProcessedInput = lastProcessedInput,
                    Components = fullState ?? [],
                    RemovedEntities = removed,
                });
                continue;
            }

            _hub.Send(clientId, new WorldSnapshot
            {
                TickNumber = SimTick,
                LastProcessedInput = lastProcessedInput,
                Components = components,
                RemovedEntities = removed,
            });
        }

        //  Drop full-sync requests whose client disconnected before the publish.
        _fullSync.Clear();
    }

    private void ApplyComponent(DataStore store, ComponentSnapshot snapshot)
    {
        if (!NetworkRegistry.TryGetInfo(Uuid.FromValue(snapshot.TypeUuid), out NetworkComponentInfo info))
        {
            _logger.LogWarning("Ignoring component snapshot with unknown type uuid {uuid}.", snapshot.TypeUuid);
            return;
        }

        //  Server-owned state is server-authored; inbound server-owned payloads are never accepted.
        if (info.Direction != NetworkDirection.ClientOwned)
        {
            return;
        }

        Uuid entityUuid = Uuid.FromValue(snapshot.Entity);
        if (!store.TryGet(entityUuid, out int entity))
        {
            entity = store.Alloc(entityUuid);
        }

        info.Codec.Apply(store, entity, snapshot.Payload);

        if (info.Type == typeof(InputComponent))
        {
            if (!store.TryGet<NetworkComponent>(entity, out _))
            {
                return;
            }

            store.QueryRef<NetworkComponent>(entity, 0f, (float _, DataStore s, int e, ref Ref<NetworkComponent> net) =>
            {
                net.Write.StagedInputs ??= new InputStageBuffer();
                if (s.TryGet(e, out InputComponent input))
                {
                    net.Write.StagedInputs.Stage(input);
                }

                if (input.ServerTickAtSample > net.Read.LastAckedInput)
                {
                    net.Write.LastAckedInput = input.ServerTickAtSample;
                }

                net.Write.LastAckedSnapshot = input.ServerTickAtSample;
            });
        }

        if (info.Type == typeof(InteractionEvent))
        {
            if (!store.TryGet<NetworkComponent>(entity, out _))
            {
                return;
            }

            store.QueryRef<NetworkComponent>(entity, 0f, (float _, DataStore s, int e, ref Ref<NetworkComponent> net) =>
            {
                net.Write.StagedInteractions ??= new InteractionStageBuffer();
                if (s.TryGet(e, out InteractionEvent interaction))
                {
                    net.Write.StagedInteractions.Stage(interaction);
                }
            });
        }
    }

    private struct OnTickAction : IForEach<NetworkComponent>
    {
        public NetworkReplicationSystem Owner;

        public void Execute(float delta, DataStore store, int entity, in NetworkComponent net)
        {
            Uuid entityUuid = store.GetUuid(entity);

            foreach (NetworkComponentInfo info in NetworkRegistry.GetComponents(NetworkDirection.ServerOwned))
            {
                if (!store.IsDirty(info.Type, entity))
                {
                    continue;
                }

                byte[] payload = info.Codec.Serialize(store, entity);
                store.ClearDirty(info.Type, entity);

                if (payload.Length == 0)
                {
                    continue;
                }

                Owner._pending.Add(new ComponentSnapshot(entityUuid.ToValue(), info.Uuid.ToValue(), payload));
            }
        }
    }

    /// <summary>
    ///     Collects the full server-owned state of every <see cref="NetworkComponent"/> entity, ignoring
    ///     dirty markings. Serializes only components the entity actually carries (the runtime-type checks
    ///     so a shared registry never emits empty-payload snapshots for absent component kinds). Read-only:
    ///     dirty flags stay untouched and are cleared solely by the delta stage.
    /// </summary>
    private struct CollectFullStateAction : IForEach<NetworkComponent>
    {
        public readonly List<ComponentSnapshot> Components;

        public CollectFullStateAction()
        {
            Components = [];
        }

        public void Execute(float delta, DataStore store, int entity, in NetworkComponent net)
        {
            Uuid entityUuid = store.GetUuid(entity);
            Span<IDataComponent> present = store.Get(entity);

            foreach (NetworkComponentInfo info in NetworkRegistry.GetComponents(NetworkDirection.ServerOwned))
            {
                if (!Contains(present, info.Type))
                {
                    continue;
                }

                byte[] payload = info.Codec.Serialize(store, entity);
                Components.Add(new ComponentSnapshot(entityUuid.ToValue(), info.Uuid.ToValue(), payload));
            }
        }

        private static bool Contains(ReadOnlySpan<IDataComponent> components, Type type)
        {
            for (var i = 0; i < components.Length; i++)
            {
                if (components[i].GetType() == type)
                {
                    return true;
                }
            }

            return false;
        }
    }
}

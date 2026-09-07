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

        if (_pending.Count == 0 && _removed.Count == 0)
        {
            return;
        }

        ComponentSnapshot[] components = _pending.ToArray();
        ulong[] removed = _removed.ToArray();
        _removed.Clear();

        foreach ((Uuid clientId, _) in _hub.Clients)
        {
            uint lastProcessedInput = 0;
            if (_sessions.TryGetEntity(clientId, out int entity)
                && store.TryGet(entity, out NetworkComponent net))
            {
                lastProcessedInput = net.LastAckedInput;
            }

            _hub.Send(clientId, new WorldSnapshot
            {
                TickNumber = SimTick,
                LastProcessedInput = lastProcessedInput,
                Components = components,
                RemovedEntities = removed,
            });
        }
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
                Owner._pending.Add(new ComponentSnapshot(entityUuid.ToValue(), info.Uuid.ToValue(), payload));
                store.ClearDirty(info.Type, entity);
            }
        }
    }
}
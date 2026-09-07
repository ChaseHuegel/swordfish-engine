using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Components;
using WaywardBeyond.Shared.Networking.Registry;
using WaywardBeyond.Shared.Networking.Transport;

namespace WaywardBeyond.Server.Core.Systems;

/// <summary>
/// Server-side replication, split into an ordered <see cref="ApplyStage"/> and <see cref="PublishStage"/>
/// so the server can consume physics + the shared motion step between them. <see cref="ApplyStage"/>
/// drains inbound client-owned components, staging each <see cref="InputComponent"/> in the server
/// entity's sim-tick-keyed command buffer. <see cref="PublishStage"/> publishes authoritative
/// server-owned snapshots plus despawns for any entity carrying a <see cref="NetworkComponent"/>.
/// Snapshot <see cref="WorldSnapshot.TickNumber"/> is the server's current sim tick (physics-step
/// ordinal), set via <see cref="SimTick"/>.
/// </summary>
public sealed class NetworkReplicationSystem : IEntitySystem
{
    private readonly IServerConnection _transport;
    private readonly ILogger<NetworkReplicationSystem> _logger;

    private readonly List<ComponentSnapshot> _pending = [];
    private readonly List<ulong> _removed = [];

    public uint SimTick { get; set; }

    public NetworkReplicationSystem(
        in IServerConnection transport,
        in ILogger<NetworkReplicationSystem> logger
    ) {
        _transport = transport;
        _logger = logger;
    }

    public void Tick(float delta, DataStore store)
    {
        ApplyStage(delta, store);
        PublishStage(delta, store);
    }

    /// <summary>Drains and applies inbound client-owned components.</summary>
    public void ApplyStage(float delta, DataStore store)
    {
        Result<WorldSnapshot> receiveResult;
        while ((receiveResult = _transport.Receive<WorldSnapshot>()).Success)
        {
            WorldSnapshot snapshot = receiveResult.Value;
            ComponentSnapshot[] components = snapshot.Components;
            for (var i = 0; i < components.Length; i++)
            {
                ApplyComponent(store, components[i]);
            }
        }
    }

    /// <summary>Collects and publishes authoritative server-owned snapshots plus despawns.</summary>
    public void PublishStage(float delta, DataStore store)
    {
        _pending.Clear();
        _removed.Clear();

        OnTickAction onTick = new() { Owner = this };
        store.Query<NetworkComponent, OnTickAction>(delta, ref onTick);

        OnRemovedAction onRemoved = new() { Owner = this };
        store.QueryRemoved<NetworkComponent, OnRemovedAction>(0f, ref onRemoved);

        if (_pending.Count == 0 && _removed.Count == 0)
        {
            return;
        }

        uint lastProcessedInput = 0;
        for (var i = 0; i < _pending.Count; i++)
        {
            ComponentSnapshot snap = _pending[i];
            if (store.TryGet(Uuid.FromValue(snap.Entity), out int entity)
                && store.TryGet(entity, out NetworkComponent net)
                && net.LastAckedInput > lastProcessedInput)
            {
                lastProcessedInput = net.LastAckedInput;
            }
        }

        var snapshot = new WorldSnapshot
        {
            TickNumber = SimTick,
            LastProcessedInput = lastProcessedInput,
            Components = _pending.ToArray(),
            RemovedEntities = _removed.ToArray(),
        };

        _transport.Send(snapshot);
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

    private struct OnRemovedAction : IForEach<NetworkComponent>
    {
        public NetworkReplicationSystem Owner;

        public void Execute(float delta, DataStore store, int entity, in NetworkComponent net)
        {
            Owner._removed.Add(store.GetUuid(entity).ToValue());

            store.ClearDirty<NetworkComponent>(entity);
            foreach (NetworkComponentInfo info in NetworkRegistry.GetComponents(NetworkDirection.ServerOwned))
            {
                store.ClearDirty(info.Type, entity);
            }
        }
    }
}
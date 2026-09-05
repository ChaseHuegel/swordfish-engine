using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.Extensions.Logging;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Components;
using WaywardBeyond.Shared.Networking.Registry;
using WaywardBeyond.Shared.Networking.Transport;

namespace WaywardBeyond.Server.Core.Systems;

/// <summary>
/// Server-side replication. Applies inbound components and publishes authoritative server-owned
/// snapshots plus despawns for any entity carrying a <see cref="NetworkComponent"/>, driven by ECS
/// dirty tracking. Inbound client-owned snapshots on unknown entities materialize a mirror; the single
/// accepted server-owned inbound is the client's initial transform placement for a freshly spawned
/// player (which is never echoed back to its owner).
/// </summary>
public sealed class NetworkReplicationSystem : IEntitySystem
{
    private readonly IServerConnection _transport;
    private readonly ServerPlayerOwnership _ownership;
    private readonly ILogger<NetworkReplicationSystem> _logger;

    private readonly List<ComponentSnapshot> _pending = [];
    private readonly List<ulong> _removed = [];
    private uint _tickNumber;

    public NetworkReplicationSystem(
        in IServerConnection transport,
        in ServerPlayerOwnership ownership,
        in ILogger<NetworkReplicationSystem> logger
    ) {
        _transport = transport;
        _ownership = ownership;
        _logger = logger;
    }

    public void Tick(float delta, DataStore store)
    {
        ApplyInbound(store);

        _tickNumber++;
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
            TickNumber = _tickNumber,
            LastProcessedInput = lastProcessedInput,
            Components = _pending.ToArray(),
            RemovedEntities = _removed.ToArray(),
        };

        _transport.Send(snapshot);
    }

    private void ApplyInbound(DataStore store)
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

    private void ApplyComponent(DataStore store, ComponentSnapshot snapshot)
    {
        if (!NetworkRegistry.TryGetInfo(Uuid.FromValue(snapshot.TypeUuid), out NetworkComponentInfo info))
        {
            _logger.LogWarning("Ignoring component snapshot with unknown type uuid {uuid}.", snapshot.TypeUuid);
            return;
        }

        if (info.Direction != NetworkDirection.ClientOwned)
        {
            ApplyPlacement(store, info, snapshot);
            return;
        }

        Uuid entityUuid = Uuid.FromValue(snapshot.Entity);
        if (!store.TryGet(entityUuid, out int entity))
        {
            //  The client authored this entity; materialize a server-side mirror. Mirrors carry no
            //  NetworkComponent, so they are never replicated downstream to other clients.
            entity = store.Alloc(entityUuid);
        }

        info.Codec.Apply(store, entity, snapshot.Payload);

        if (info.Type == typeof(InputComponent)
            && store.TryGet(entity, out InputComponent input))
        {
            store.QueryRef<NetworkComponent>(entity, 0f, (float _, DataStore s, int e, ref Ref<NetworkComponent> net) =>
            {
                if (input.SequenceNumber > net.Read.LastAckedInput)
                {
                    net.Write.LastAckedInput = input.SequenceNumber;
                }

                net.Write.LastAckedSnapshot = input.ServerTickAtSample;
            });
        }
    }

    /// <summary>
    /// The only accepted server-owned inbound is the client's initial transform placement for a freshly
    /// spawned player whose mirror exists but has no transform yet. Subsequent server-owned inbound is
    /// ignored so clients cannot author authoritative state.
    /// </summary>
    private void ApplyPlacement(DataStore store, NetworkComponentInfo info, ComponentSnapshot snapshot)
    {
        if (info.Type != typeof(TransformComponent))
        {
            return;
        }

        Uuid entityUuid = Uuid.FromValue(snapshot.Entity);
        if (!store.TryGet(entityUuid, out int entity)
            || !store.TryGet<NetworkComponent>(entity, out _)
            || store.TryGet<TransformComponent>(entity, out _))
        {
            return;
        }

        TransformMessage message = TransformMessage.Deserialize(snapshot.Payload);
        store.AddOrUpdate(entity, new TransformComponent(
            new Vector3(message.PositionX, message.PositionY, message.PositionZ),
            new Quaternion(message.OrientationX, message.OrientationY, message.OrientationZ, message.OrientationW),
            new Vector3(message.ScaleX, message.ScaleY, message.ScaleZ)
        ));
        store.ClearDirty(typeof(TransformComponent), entity);
    }

    private struct OnTickAction : IForEach<NetworkComponent>
    {
        public NetworkReplicationSystem Owner;

        public void Execute(float delta, DataStore store, int entity, in NetworkComponent net)
        {
            Uuid entityUuid = store.GetUuid(entity);
            Uuid owned = Owner._ownership.GetOwnedPlayer() ?? Uuid.Null;

            foreach (NetworkComponentInfo info in NetworkRegistry.GetComponents(NetworkDirection.ServerOwned))
            {
                if (!store.IsDirty(info.Type, entity))
                {
                    continue;
                }

                //  Don't echo the owner's placed transform back to the owner; local client motion is authoritative.
                if (info.Type == typeof(TransformComponent) && owned != Uuid.Null && owned == entityUuid)
                {
                    store.ClearDirty(info.Type, entity);
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
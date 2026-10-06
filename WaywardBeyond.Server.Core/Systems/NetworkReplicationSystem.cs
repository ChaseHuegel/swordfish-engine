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
/// drains inbound client-owned components across every connected client and binds each snapshot to the
/// sender's session entity: a wire uuid naming any other entity is ignored, staging each
/// <see cref="InputComponent"/> in the server entity's sim-tick-keyed command buffer. <see cref="PublishStage"/>
/// publishes authoritative server-owned snapshots plus despawns to each client, composing a per-client
/// <see cref="WorldSnapshot"/> whose <see cref="WorldSnapshot.LastProcessedInput"/> reflects that client
///'s own acked input. Clients whose join stream is in flight (see <see cref="BeginStream"/>) receive no
/// per-tick deltas until it completes, avoiding a Loading-time snapshot pile-up. Snapshot
/// <see cref="WorldSnapshot.TickNumber"/> is the server's current sim tick
/// (physics-step ordinal), set via <see cref="SimTick"/>.
/// </summary>
public sealed class NetworkReplicationSystem : IEntitySystem
{
    private readonly ServerConnectionHub _hub;
    private readonly SessionManager _sessions;
    private readonly ILogger<NetworkReplicationSystem> _logger;

    private readonly List<ComponentSnapshot> _pending = [];
    private readonly List<ComponentRemoval> _pendingRemovals = [];
    private readonly Queue<ulong> _removed = [];

    //  Clients awaiting a one-shot full-state snapshot (full server-owned state of every networked
    //  entity) on their next publish, in place of that tick's delta. Newly joined clients request it so
    //  pre-existing players - whose dirty markings were already published-and-cleared before the client
    //  connected - still materialize as remote players.
    private readonly HashSet<Uuid> _fullSync = [];

    //  Clients whose join world stream is in flight: they receive no per-tick deltas (which would pile
    //  up unapplied while the client is Loading) until the stream complete has been enqueued. The
    //  full-sync publish is exempt and remains the client's first snapshot.
    private readonly HashSet<Uuid> _streamingClients = [];

    //  Stream-complete notices queued by the join system; applied at the END of the publish stage so
    //  the tick that enqueues the complete still publishes no deltas.
    private readonly Queue<Uuid> _streamEnds = new();

    public uint SimTick { get; set; }

    private readonly uint _snapshotIntervalTicks;

    //  The server-owned component enumeration is cached per system instance: the replication hot path
    //  runs at least once per tick, and the registry's per-call list allocation would otherwise scale
    //  with world size instead of dirtiness.
    private readonly NetworkComponentInfo[] _serverOwnedComponents;

    //  The wire type tag (WorldSnapshot FullName) and its byte length, pre-encoded for snapshot framing.
    private readonly byte[] _snapshotTypeTag;

    public NetworkReplicationSystem(
        in ServerConnectionHub hub,
        SessionManager sessions,
        in ILogger<NetworkReplicationSystem> logger,
        int snapshotHz = 0
    ) {
        _hub = hub;
        _sessions = sessions;
        _logger = logger;
        //  0 = uncapped (every tick); the server wiring passes the configured cadence.
        _snapshotIntervalTicks = snapshotHz <= 0 ? 1 : (uint)Math.Max(1, 60 / snapshotHz);
        _serverOwnedComponents = [.. NetworkRegistry.GetComponents(NetworkDirection.ServerOwned)];
        _snapshotTypeTag = System.Text.Encoding.UTF8.GetBytes(typeof(WorldSnapshot).FullName!);
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

    /// <summary>Marks a client's join world stream as in flight: no per-tick deltas until <see cref="EndStream"/>.</summary>
    public void BeginStream(Uuid clientId)
    {
        _streamingClients.Add(clientId);
    }

    /// <summary>
    /// Records that a client's <c>WorldStreamComplete</c> has been enqueued; the streaming gate lifts
    /// at the end of the current publish stage, so the join tick itself publishes nothing to it.
    /// </summary>
    public void EndStream(Uuid clientId)
    {
        _streamEnds.Enqueue(clientId);
    }

    public void Tick(float delta, DataStore store)
    {
        ApplyStage(delta, store);
        PublishStage(delta, store);
    }

    /// <summary>
    /// Drains and applies inbound client-owned components from all connected clients. Every snapshot is
    /// bound to the sender's session entity: a component whose wire uuid is not the sender's own player
    /// entity is ignored and never allocated, staged, or applied. A malformed snapshot from one client is
    /// logged and skipped; it never aborts the world tick for the other clients.
    /// </summary>
    public void ApplyStage(float delta, DataStore store)
    {
        foreach ((Uuid clientId, WorldSnapshot snapshot) in _hub.Receive<WorldSnapshot>())
        {
            try
            {
                ApplyClientSnapshot(store, clientId, snapshot);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dropping malformed inbound snapshot from client {clientId}.", clientId);
            }
        }
    }

    private void ApplyClientSnapshot(DataStore store, Uuid clientId, WorldSnapshot snapshot)
    {
        if (!_sessions.TryGetEntity(clientId, out int sessionEntity))
        {
            _logger.LogWarning("Ignoring inbound snapshot from client {clientId} without a session.", clientId);
            return;
        }

        Uuid sessionUuid = store.GetUuid(sessionEntity);
        if (sessionUuid == Uuid.Null)
        {
            //  The session entity was freed without clearing its session. Drop the stale mapping so
            //  the client is no longer treated as joined.
            _sessions.EndSession(clientId);
            _logger.LogWarning("Dropping stale session for client {clientId}; its entity was freed.", clientId);
            return;
        }

        ComponentSnapshot[] components = snapshot.Components;
        for (var i = 0; i < components.Length; i++)
        {
            ApplyComponent(store, sessionEntity, sessionUuid, components[i]);
        }
    }

    /// <summary>
    /// Collects authoritative server-owned snapshots once, then publishes to each client a per-client
    /// snapshot carrying that client's own <see cref="WorldSnapshot.LastProcessedInput"/>. Despawns are
    /// those queued via <see cref="RequestDespawn"/>. Publishes at the configured snapshot cadence
    /// (<c>SnapshotHz</c>): the tick semantics are unchanged and despawns/full-syncs ride the same
    /// cadence, at most one interval of delay.
    /// </summary>
    public void PublishStage(float delta, DataStore store)
    {
        //  Snapshot cadence: emit only on interval ticks (60 / SnapshotHz). All publish effects ride
        //  the cadence, so nothing on the wire waits more than one interval.
        if (SimTick % _snapshotIntervalTicks != 0)
        {
            return;
        }

        _pending.Clear();
        _pendingRemovals.Clear();

        OnTickAction onTick = new() { Owner = this };
        store.Query<NetworkComponent, OnTickAction>(delta, ref onTick);

        if (_pending.Count == 0 && _pendingRemovals.Count == 0 && _removed.Count == 0 && _fullSync.Count == 0)
        {
            return;
        }

        ComponentSnapshot[] components = _pending.ToArray();
        ComponentRemoval[] removedComponents = _pendingRemovals.ToArray();
        ulong[] removed = _removed.ToArray();
        _removed.Clear();

        //  Serialized once per tick: the authoritative component set is shared by every client, so the
        //  whole snapshot frame is built a single time and fanned out with a per-client ack overwrite
        //  (LastProcessedInput lives at a fixed 4-byte offset right after TickNumber).
        byte[] sharedFrame = BuildSnapshotFrame(SimTick, 0, components, removed, removedComponents);
        int payloadOffset = 8 + _snapshotTypeTag.Length;

        ComponentSnapshot[]? fullState = null;
        if (_fullSync.Count > 0)
        {
            //  Read-only snapshot of every server-owned component on every networked entity, regardless
            //  of dirty, for clients that joined after those components were last published.
            CollectFullStateAction collect = new(_serverOwnedComponents);
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
                    RemovedComponents = removedComponents,
                });
                continue;
            }

            //  A joining client's stream is still in flight: no per-tick deltas until the stream
            //  complete has been enqueued (the full-sync publish above remains its first snapshot).
            if (_streamingClients.Contains(clientId))
            {
                continue;
            }

            //  Per-client copy of the shared frame with that client's own ack (and tick) rewritten.
            //  The nsd payload is self-describing [field-id][value]: TickNumber sits at payload + 2,
            //  its value is 4 bytes, then the 2-byte LastProcessedInput field id, then its 4-byte value
            //  at payload + 8.
            byte[] clientFrame = (byte[])sharedFrame.Clone();
            BitConverter.TryWriteBytes(clientFrame.AsSpan(payloadOffset + 2, 4), SimTick);
            BitConverter.TryWriteBytes(clientFrame.AsSpan(payloadOffset + 8, 4), lastProcessedInput);
            _hub.SendRaw(clientId, clientFrame);
        }

        //  Lift the streaming gates recorded this tick, so the next publish sends deltas again.
        while (_streamEnds.TryDequeue(out Uuid clientId))
        {
            _streamingClients.Remove(clientId);
        }

        //  Drop full-sync requests whose client disconnected before the publish.
        _fullSync.Clear();
    }

    private void ApplyComponent(DataStore store, int sessionEntity, Uuid sessionUuid, ComponentSnapshot snapshot)
    {
        Uuid entityUuid = Uuid.FromValue(snapshot.Entity);
        if (entityUuid != sessionUuid)
        {
            _logger.LogWarning("Ignoring client-owned snapshot addressed at entity {uuid}; only the sender's session entity {sessionUuid} is writable.", snapshot.Entity, sessionUuid);
            return;
        }

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

        //  The session entity already exists (spawned at join); wire uuids never allocate entities.
        info.Codec.Apply(store, sessionEntity, snapshot.Payload);

        if (info.Type == typeof(InputComponent))
        {
            if (!store.TryGet<NetworkComponent>(sessionEntity, out _))
            {
                return;
            }

            store.QueryRef<NetworkComponent>(sessionEntity, 0f, (float _, DataStore s, int e, ref Ref<NetworkComponent> net) =>
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
            if (!store.TryGet<NetworkComponent>(sessionEntity, out _))
            {
                return;
            }

            store.QueryRef<NetworkComponent>(sessionEntity, 0f, (float _, DataStore s, int e, ref Ref<NetworkComponent> net) =>
            {
                net.Write.StagedInteractions ??= new InteractionStageBuffer();
                if (s.TryGet(e, out InteractionEvent interaction))
                {
                    net.Write.StagedInteractions.Stage(interaction);
                }
            });
        }

        if (info.Type == typeof(InventoryEvent))
        {
            if (!store.TryGet<NetworkComponent>(sessionEntity, out _))
            {
                return;
            }

            store.QueryRef<NetworkComponent>(sessionEntity, 0f, (float _, DataStore s, int e, ref Ref<NetworkComponent> net) =>
            {
                net.Write.StagedInventoryOps ??= new InventoryOpStageBuffer();
                if (s.TryGet(e, out InventoryEvent inventoryEvent) && inventoryEvent.SlotMove != null)
                {
                    net.Write.StagedInventoryOps.Stage(inventoryEvent.SequenceNumber, inventoryEvent.SlotMove.Value);
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

            foreach (NetworkComponentInfo info in Owner._serverOwnedComponents)
            {
                if (!store.IsDirty(info.Type, entity))
                {
                    continue;
                }

                //  Removal publishes as a ComponentRemoval when the component no longer exists; the
                //  dirty flag is cleared for both outcomes, and never both in one tick.
                if (!store.Has(info.Type, entity))
                {
                    store.ClearDirty(info.Type, entity);
                    Owner._pendingRemovals.Add(new ComponentRemoval(entityUuid.ToValue(), info.Uuid.ToValue()));
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
    private byte[] BuildSnapshotFrame(uint tick, uint lastProcessedInput, ComponentSnapshot[] components, ulong[] removed, ComponentRemoval[] removedComponents)
    {
        byte[] payload = new WorldSnapshot
        {
            TickNumber = tick,
            LastProcessedInput = lastProcessedInput,
            Components = components,
            RemovedEntities = removed,
            RemovedComponents = removedComponents,
        }.Serialize();

        int bodyLength = 4 + _snapshotTypeTag.Length + payload.Length;
        var frame = new byte[4 + bodyLength];
        BitConverter.TryWriteBytes(frame.AsSpan(0, 4), bodyLength);
        BitConverter.TryWriteBytes(frame.AsSpan(4, 4), _snapshotTypeTag.Length);
        _snapshotTypeTag.CopyTo(frame, 8);
        payload.CopyTo(frame, 8 + _snapshotTypeTag.Length);
        return frame;
    }

    private struct CollectFullStateAction : IForEach<NetworkComponent>
    {
        public readonly List<ComponentSnapshot> Components;
        public NetworkComponentInfo[] ServerOwned;

        public CollectFullStateAction(NetworkComponentInfo[] serverOwned)
        {
            Components = [];
            ServerOwned = serverOwned;
        }

        public void Execute(float delta, DataStore store, int entity, in NetworkComponent net)
        {
            Uuid entityUuid = store.GetUuid(entity);
            Span<IDataComponent> present = store.Get(entity);

            //  `store.Get` boxes per entity; this path is join-time-only (one-shot per joining client),
            //  so the boxing is bounded by joins, not by the per-tick hot path.
            foreach (NetworkComponentInfo info in ServerOwned)
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

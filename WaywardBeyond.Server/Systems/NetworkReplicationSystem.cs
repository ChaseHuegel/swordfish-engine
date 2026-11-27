using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Krill;
using Swordfish.Library.Util;
using WaywardBeyond.Config;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Data.Networking;
using WaywardBeyond.Networking.Registry;
using WaywardBeyond.Gameplay.Networking;
using WaywardBeyond.Networking.Rpc;

namespace WaywardBeyond.Server.Systems;

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
public sealed class NetworkReplicationSystem
{
    private const int MAX_INPUT_PAINT_ID_LENGTH = 128;

    private readonly ServerPeers _peers;
    private readonly SessionManager _sessions;
    private readonly ILogger<NetworkReplicationSystem> _logger;
    private readonly NetworkRegistry _registry;

    private readonly List<PendingSnapshot> _pending = [];
    private readonly List<PendingRemoval> _pendingRemovals = [];
    private readonly List<PendingSnapshot> _fullSyncPending = [];
    private readonly Queue<ulong> _removed = [];

    private readonly List<ComponentSnapshot> _filteredComponents = [];
    private readonly List<ComponentRemoval> _filteredRemovals = [];

    //  Publish buffers. A WorldSnapshot is serialized synchronously inside the publish loop and then
    //  dropped, so the arrays can be reused across publishes when the counts hold steady.
    private ComponentSnapshot[] _pendingSnapshot = [];
    private ComponentSnapshot[] _filteredComponentSnapshot = [];
    private ComponentRemoval[] _removalSnapshot = [];
    private ComponentRemoval[] _filteredRemovalSnapshot = [];
    private ulong[] _removedSnapshot = [];

    private bool _pendingHasRecipientFilters;
    private bool _pendingRemovalsHasRecipientFilters;
    private bool _fullSyncHasRecipientFilters;

    //  Clients awaiting a one-shot full-state snapshot (full server-owned state of every networked
    //  entity) on their next publish, in place of that tick's delta. Newly joined clients request it so
    //  pre-existing players - whose dirty markings were already published-and-cleared before the client
    //  connected - still materialize as remote players.
    private readonly HashSet<Uuid> _fullSync = [];

    //  Clients whose join world stream is in flight: they receive no per-tick deltas (which would pile
    //  up unapplied while the client is Loading) until the stream complete has been enqueued. The
    //  full-sync publish is exempt and remains the client's first snapshot. Each entry carries the tick
    //  time the gate opened, so a client that never starts or finishes its stream is reaped.
    private readonly Dictionary<Uuid, long> _streamingClients = [];
    private readonly List<Uuid> _expiredStreams = [];

    //  Stream-complete notices queued by the join system; drained at the end of every publish stage
    //  so the tick that enqueues the complete still publishes no deltas, even on an idle tick.
    private readonly HashSet<Uuid> _streamEnds = [];

    /// <summary>The sim tick stamped on the next published snapshot. Set by the publish system.</summary>
    public uint SimTick { get; set; }

    private readonly NetworkingConfig _config;
    private float _sincePublish;

    //  The server-owned component enumeration is cached per system instance: the replication hot path
    //  runs at least once per tick, and the registry's per-call list allocation would otherwise scale
    //  with world size instead of dirtiness.
    private readonly NetworkComponentInfo[] _serverOwnedComponents;

    private uint _nextBatchId;

    /// <summary>Creates the replication stages for one world.</summary>
    public NetworkReplicationSystem(
        ServerPeers peers,
        SessionManager sessions,
        ILogger<NetworkReplicationSystem> logger,
        NetworkingConfig config,
        NetworkRegistry registry
    ) {
        _peers = peers;
        _sessions = sessions;
        _logger = logger;
        _config = config;
        _registry = registry;
        _serverOwnedComponents = registry.GetComponents(NetworkDirection.ServerOwned);
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
        _streamingClients[clientId] = Environment.TickCount64;
        _streamEnds.Remove(clientId);
    }

    /// <summary>
    /// Records that a client's <c>LevelStreamComplete</c> has been enqueued; the streaming gate lifts
    /// at the end of the current publish stage, so the join tick itself publishes nothing to it.
    /// </summary>
    public void EndStream(Uuid clientId)
    {
        if (!_streamingClients.TryGetValue(clientId, out long startedMs))
        {
            return;
        }

        if (_streamEnds.Add(clientId))
        {
            _logger.LogDebug(
                "Join stream ended for client {clientId} after {elapsed} ms; queued gate lift.",
                clientId, Environment.TickCount64 - startedMs);
        }
    }

    /// <summary>
    /// Lifts a client's streaming gate immediately, without a publish. Called when a client leaves or
    /// disconnects before its stream completes, so the gate table cannot grow with abandoned joins.
    /// </summary>
    public void CancelStream(Uuid clientId)
    {
        _streamingClients.Remove(clientId);
        _streamEnds.Remove(clientId);
        _fullSync.Remove(clientId);
    }

    /// <summary>The number of clients whose join stream is still gated.</summary>
    internal int StreamingClientCount => _streamingClients.Count;

    /// <summary>Whether <paramref name="clientId"/> is still receiving its initial level stream.</summary>
    internal bool IsStreaming(Uuid clientId) => _streamingClients.ContainsKey(clientId);

    private void ExpireStalledStreams()
    {
        if (_streamingClients.Count == 0)
        {
            return;
        }

        int timeoutMs = Math.Max(1, _config.Protocol.JoinStreamTimeoutMs.Get());
        long now = Environment.TickCount64;

        _expiredStreams.Clear();
        foreach ((Uuid clientId, long startedMs) in _streamingClients)
        {
            long elapsedMs = now - startedMs;
            if (elapsedMs < timeoutMs)
            {
                continue;
            }

            if (_streamEnds.Contains(clientId))
            {
                _logger.LogWarning(
                    "Client {clientId} completed its join stream after {elapsed} ms (timeout {timeout} ms); lifting the pending gate instead of closing the peer.",
                    clientId, elapsedMs, timeoutMs);
                continue;
            }

            _expiredStreams.Add(clientId);
        }

        for (var index = 0; index < _expiredStreams.Count; index++)
        {
            Uuid clientId = _expiredStreams[index];
            long elapsedMs = _streamingClients.Remove(clientId, out long startedMs) ? now - startedMs : timeoutMs;
            _fullSync.Remove(clientId);

            if (_peers.TryGet(clientId, out ServerPeer peer))
            {
                _logger.LogWarning(
                    "Client {clientId} did not complete its join stream within {timeout} ms (gate open {elapsed} ms, EndStream not received); closing the peer.",
                    clientId, timeoutMs, elapsedMs);
                peer.Rpc.Close();
            }
        }
    }

    /// <summary>
    /// Drains and applies inbound client-owned components from all connected clients. Every snapshot is
    /// bound to the sender's session entity: a component whose wire uuid is not the sender's own player
    /// entity is ignored and never allocated, staged, or applied. A malformed snapshot from one client is
    /// logged and skipped; it never aborts the world tick for the other clients.
    /// </summary>
    public void ApplyStage(float delta, DataStore store)
    {
        foreach (Uuid clientId in _sessions.Clients)
        {
            if (!_peers.TryGet(clientId, out ServerPeer peer))
            {
                continue;
            }

            while (peer.Snapshots.TryDequeue(out WorldSnapshot snapshot))
            {
                try
                {
                    ApplyClientSnapshot(store, peer.GameId, snapshot);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Dropping malformed inbound snapshot from client {clientId}.", peer.GameId);
                }
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
    /// (<see cref="NetworkingConfig.Protocol.SnapshotHz"/>), measured in wall-clock time: the tick semantics are
    /// unchanged and despawns/full-syncs ride the same cadence, at most one interval of delay.
    /// </summary>
    public void PublishStage(float delta, DataStore store)
    {
        //  A client that joined but never streamed would otherwise hold its gate forever. Reap it on
        //  the tick, not the publish cadence, so the check runs even when nothing else is published.
        ExpireStalledStreams();

        PublishSnapshots(delta, store);
        DrainStreamEnds();
    }

    private void PublishSnapshots(float delta, DataStore store)
    {
        //  Snapshot cadence: publish once per SnapshotHz, measured from the tick deltas so it does not
        //  depend on the server tick rate. Subtracting, not resetting, keeps the average exact. All
        //  publish effects ride the cadence, so nothing on the wire waits more than one interval.
        float interval = 1f / Math.Max(1, _config.Protocol.SnapshotHz.Get());
        _sincePublish += delta;
        if (_sincePublish < interval)
        {
            return;
        }

        _sincePublish -= interval;

        _pending.Clear();
        _pendingRemovals.Clear();
        _pendingHasRecipientFilters = false;
        _pendingRemovalsHasRecipientFilters = false;

        OnTickAction onTick = new() { Owner = this };
        store.Query<NetworkComponent, OnTickAction>(delta, ref onTick);

        if (_pending.Count == 0 && _pendingRemovals.Count == 0 && _removed.Count == 0 && _fullSync.Count == 0)
        {
            return;
        }

        ComponentSnapshot[]? sharedComponents = _pendingHasRecipientFilters
            ? null
            : CopySnapshots(_pending, ref _pendingSnapshot);
        ComponentRemoval[]? sharedRemovals = _pendingRemovalsHasRecipientFilters
            ? null
            : CopyRemovals(_pendingRemovals, ref _removalSnapshot);
        ulong[] removed = ToExactArray(_removed, ref _removedSnapshot);
        _removed.Clear();

        ComponentSnapshot[]? sharedFullState = null;
        if (_fullSync.Count > 0)
        {
            //  Read-only snapshot of every server-owned component on every networked entity, regardless
            //  of dirty, for clients that joined after those components were last published.
            _fullSyncPending.Clear();
            _fullSyncHasRecipientFilters = false;
            CollectFullStateAction collect = new() { Owner = this };
            store.Query<NetworkComponent, CollectFullStateAction>(delta, ref collect);
            if (!_fullSyncHasRecipientFilters)
            {
                sharedFullState = CopySnapshots(_fullSyncPending);
            }
        }

        foreach (Uuid clientId in _sessions.Clients)
        {
            if (!_peers.TryGet(clientId, out ServerPeer peer))
            {
                continue;
            }

            bool isFullSync = _fullSync.Remove(clientId);

            //  A joining client's stream is still in flight: no per-tick deltas until the stream
            //  complete has been enqueued (the full-sync publish remains its first snapshot).
            if (!isFullSync && _streamingClients.ContainsKey(clientId))
            {
                continue;
            }

            uint lastProcessedInput = 0;
            int recipientEntity = Entity.Null;
            Uuid recipientEntityUuid = Uuid.Null;
            if (_sessions.TryGetEntity(clientId, out int sessionEntity) && sessionEntity != Entity.Null)
            {
                Uuid sessionUuid = store.GetUuid(sessionEntity);
                if (sessionUuid != Uuid.Null)
                {
                    recipientEntity = sessionEntity;
                    recipientEntityUuid = sessionUuid;
                    if (store.TryGet(sessionEntity, out NetworkComponent net))
                    {
                        lastProcessedInput = net.LastAckedInput;
                    }
                }
            }

            var recipient = new ReplicationRecipient(clientId, recipientEntity, recipientEntityUuid);

            ComponentRemoval[] clientRemovals = sharedRemovals
                ?? FilterRemovals(store, _pendingRemovals, in recipient);

            ComponentSnapshot[] clientComponents = isFullSync
                ? sharedFullState ?? FilterFullSyncSnapshots(store, _fullSyncPending, in recipient)
                : sharedComponents ?? FilterDeltaSnapshots(store, _pending, in recipient);

            if (!isFullSync
                && clientComponents.Length == 0
                && clientRemovals.Length == 0
                && removed.Length == 0)
            {
                continue;
            }

            var snapshot = new WorldSnapshot
            {
                TickNumber = SimTick,
                LastProcessedInput = lastProcessedInput,
                Components = clientComponents,
                RemovedEntities = removed,
                RemovedComponents = clientRemovals,
            };

            if (!SendSnapshot(peer, snapshot))
            {
                _logger.LogError("Failed to send a snapshot to client {clientId}; closing the peer.", clientId);
                peer.Rpc.Close();
            }
        }

        //  Drop full-sync requests whose client disconnected before the publish and release scratch payloads.
        _fullSync.Clear();
        _fullSyncPending.Clear();
        _filteredComponents.Clear();
        _filteredRemovals.Clear();
    }

    private void DrainStreamEnds()
    {
        if (_streamEnds.Count == 0)
        {
            return;
        }

        long now = Environment.TickCount64;
        foreach (Uuid clientId in _streamEnds)
        {
            if (_streamingClients.Remove(clientId, out long startedMs))
            {
                _logger.LogDebug(
                    "Lifted join-stream gate for client {clientId} after {elapsed} ms.",
                    clientId, now - startedMs);
            }
        }

        _streamEnds.Clear();
    }

    /// <summary>
    /// Sends one snapshot. A payload over the fragment budget is split into ordered
    /// <see cref="SnapshotBatch"/> notifications. A rejected send reports failure so the caller can
    /// close the peer.
    /// </summary>
    private bool SendSnapshot(ServerPeer peer, in WorldSnapshot snapshot)
    {
        if (snapshot.GetSize() <= PayloadFragmenter.FRAGMENT_BUDGET)
        {
            return peer.Client.SendSnapshot(snapshot);
        }

        byte[] payload = snapshot.Serialize();
        uint batchId = ++_nextBatchId;
        int partCount = PayloadFragmenter.PartCount(payload.Length);
        for (var partIndex = 0; partIndex < partCount; partIndex++)
        {
            var batch = new SnapshotBatch
            {
                BatchId = batchId,
                PartIndex = (uint)partIndex,
                PartCount = (uint)partCount,
                Payload = PayloadFragmenter.Slice(payload, partIndex),
            };

            if (!peer.Client.SnapshotBatch(batch))
            {
                return false;
            }
        }

        return true;
    }

    private ComponentSnapshot[] FilterDeltaSnapshots(
        DataStore store,
        List<PendingSnapshot> source,
        in ReplicationRecipient recipient
    ) {
        PopulateFilteredSnapshots(store, source, in recipient);
        return ToExactArray(_filteredComponents, ref _filteredComponentSnapshot);
    }

    private ComponentSnapshot[] FilterFullSyncSnapshots(
        DataStore store,
        List<PendingSnapshot> source,
        in ReplicationRecipient recipient
    ) {
        PopulateFilteredSnapshots(store, source, in recipient);
        ComponentSnapshot[] copy = _filteredComponents.Count == 0 ? [] : _filteredComponents.ToArray();
        _filteredComponents.Clear();
        return copy;
    }

    private void PopulateFilteredSnapshots(
        DataStore store,
        List<PendingSnapshot> source,
        in ReplicationRecipient recipient
    ) {
        _filteredComponents.Clear();
        for (var i = 0; i < source.Count; i++)
        {
            PendingSnapshot entry = source[i];
            if (entry.RecipientFilter == null || entry.RecipientFilter(store, entry.Entity, recipient))
            {
                _filteredComponents.Add(entry.Snapshot);
            }
        }
    }

    private ComponentRemoval[] FilterRemovals(
        DataStore store,
        List<PendingRemoval> source,
        in ReplicationRecipient recipient
    ) {
        _filteredRemovals.Clear();
        for (var i = 0; i < source.Count; i++)
        {
            PendingRemoval entry = source[i];
            if (entry.RecipientFilter == null || entry.RecipientFilter(store, entry.Entity, recipient))
            {
                _filteredRemovals.Add(entry.Removal);
            }
        }

        return ToExactArray(_filteredRemovals, ref _filteredRemovalSnapshot);
    }

    private static ComponentSnapshot[] CopySnapshots(List<PendingSnapshot> source, ref ComponentSnapshot[] cache)
    {
        if (source.Count == 0)
        {
            return cache.Length == 0 ? cache : cache = [];
        }

        if (cache.Length != source.Count)
        {
            cache = new ComponentSnapshot[source.Count];
        }

        for (var i = 0; i < source.Count; i++)
        {
            cache[i] = source[i].Snapshot;
        }

        return cache;
    }

    private static ComponentSnapshot[] CopySnapshots(List<PendingSnapshot> source)
    {
        if (source.Count == 0)
        {
            return [];
        }

        var copy = new ComponentSnapshot[source.Count];
        for (var i = 0; i < source.Count; i++)
        {
            copy[i] = source[i].Snapshot;
        }

        return copy;
    }

    private static ComponentRemoval[] CopyRemovals(List<PendingRemoval> source, ref ComponentRemoval[] cache)
    {
        if (source.Count == 0)
        {
            return cache.Length == 0 ? cache : cache = [];
        }

        if (cache.Length != source.Count)
        {
            cache = new ComponentRemoval[source.Count];
        }

        for (var i = 0; i < source.Count; i++)
        {
            cache[i] = source[i].Removal;
        }

        return cache;
    }

    /// <summary>
    /// Copies a list into an exact-length array, reusing the previous array when the length matches.
    /// </summary>
    private static T[] ToExactArray<T>(List<T> source, ref T[] cache)
    {
        if (source.Count == 0)
        {
            return cache.Length == 0 ? cache : cache = [];
        }

        if (cache.Length != source.Count)
        {
            cache = new T[source.Count];
        }

        source.CopyTo(cache);
        return cache;
    }

    /// <summary>
    /// Copies a queue into an exact-length array, reusing the previous array when the length matches.
    /// </summary>
    private static T[] ToExactArray<T>(Queue<T> source, ref T[] cache)
    {
        if (source.Count == 0)
        {
            return cache.Length == 0 ? cache : cache = [];
        }

        if (cache.Length != source.Count)
        {
            cache = new T[source.Count];
        }

        source.CopyTo(cache, 0);
        return cache;
    }

    private void ApplyComponent(DataStore store, int sessionEntity, Uuid sessionUuid, ComponentSnapshot snapshot)
    {
        Uuid entityUuid = Uuid.FromValue(snapshot.Entity);
        if (entityUuid != sessionUuid)
        {
            //  Client-triggerable and unbounded: debug keeps a flood from filling the log.
            _logger.LogDebug("Ignoring client-owned snapshot addressed at entity {uuid}; only the sender's session entity {sessionUuid} is writable.", snapshot.Entity, sessionUuid);
            return;
        }

        Result<NetworkComponentInfo> lookup = _registry.GetInfo(Uuid.FromValue(snapshot.TypeUuid));
        if (!lookup.Success)
        {
            _logger.LogDebug("Ignoring component snapshot with unknown type uuid {uuid}.", snapshot.TypeUuid);
            return;
        }

        NetworkComponentInfo info = lookup.Value;

        //  Server-owned state is server-authored; inbound server-owned payloads are never accepted.
        if (info.Direction != NetworkDirection.ClientOwned)
        {
            return;
        }

        //  The session entity already exists (spawned at join); wire uuids never allocate entities.
        Result applied = info.Codec.Apply(store, sessionEntity, snapshot.Payload);
        if (!applied.Success)
        {
            _logger.LogDebug("Ignoring {component} snapshot for entity {entity}: {message}.",
                info.Type.Name, sessionEntity, applied.Message);
            return;
        }

        if (info.Type == typeof(InputComponent) && store.TryGet(sessionEntity, out InputComponent input))
        {
            if (input.PaintId != null && input.PaintId.Length > MAX_INPUT_PAINT_ID_LENGTH)
            {
                input.PaintId = null;
                store.AddOrUpdate(sessionEntity, input);
            }

            var stage = new StageInput(in input, SimTick);
            store.QueryRef<NetworkComponent, StageInput>(sessionEntity, 0f, ref stage);
        }

        if (info.Type == typeof(InteractionEvent) && store.TryGet(sessionEntity, out InteractionEvent interaction))
        {
            var stage = new StageInteraction(in interaction);
            store.Query<NetworkComponent, StageInteraction>(sessionEntity, 0f, ref stage);
        }

        if (info.Type == typeof(InventoryEvent)
            && store.TryGet(sessionEntity, out InventoryEvent inventoryEvent)
            && inventoryEvent.SlotMove != null)
        {
            var stage = new StageInventory(inventoryEvent.SequenceNumber, inventoryEvent.SlotMove.Value);
            store.Query<NetworkComponent, StageInventory>(sessionEntity, 0f, ref stage);
        }
    }

    private struct StageInput(in InputComponent input, uint stagedAtSimTick) : IForEachRef<NetworkComponent>
    {
        private readonly InputComponent _input = input;
        private readonly uint _stagedAtSimTick = stagedAtSimTick;

        public void Execute(float delta, DataStore store, int entity, ref Ref<NetworkComponent> net)
        {
            ref readonly NetworkComponent current = ref net.Read;
            if (current.StagedInputs == null && _input.ServerTickAtSample <= current.LastAckedInput)
            {
                return;
            }

            ref NetworkComponent value = ref net.Write;
            value.StagedInputs?.Stage(in _input, _stagedAtSimTick);

            if (_input.ServerTickAtSample > value.LastAckedInput)
            {
                value.LastAckedInput = _input.ServerTickAtSample;
            }
        }
    }

    private struct StageInteraction(in InteractionEvent interaction) : IForEach<NetworkComponent>
    {
        private readonly InteractionEvent _interaction = interaction;

        public void Execute(float delta, DataStore store, int entity, in NetworkComponent net)
        {
            net.StagedInteractions?.Stage(_interaction);
        }
    }

    private struct StageInventory(uint sequenceNumber, in SlotMoveOp slotMove) : IForEach<NetworkComponent>
    {
        private readonly SlotMoveOp _slotMove = slotMove;

        public void Execute(float delta, DataStore store, int entity, in NetworkComponent net)
        {
            net.StagedInventoryOps?.Stage(sequenceNumber, in _slotMove);
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

                if (info.ReplicationFilter != null && !info.ReplicationFilter(store, entity))
                {
                    continue;
                }

                //  Removal publishes as a ComponentRemoval when the component no longer exists; the
                //  dirty flag is cleared for both outcomes, and never both in one tick.
                if (!store.Has(info.Type, entity))
                {
                    store.ClearDirty(info.Type, entity);
                    Owner._pendingRemovals.Add(new PendingRemoval(
                        new ComponentRemoval(entityUuid.ToValue(), info.Uuid.ToValue()),
                        entity,
                        info.RecipientFilter
                    ));
                    Owner._pendingRemovalsHasRecipientFilters |= info.RecipientFilter != null;
                    continue;
                }

                Result<byte[]> serialize = info.Codec.Serialize(store, entity);
                store.ClearDirty(info.Type, entity);

                if (!serialize.Success)
                {
                    Owner._logger.LogDebug("Dropping {component} snapshot for entity {uuid}: {message}.",
                        info.Type.Name, entityUuid, serialize.Message);
                    continue;
                }

                Owner._pending.Add(new PendingSnapshot(
                    new ComponentSnapshot(entityUuid.ToValue(), info.Uuid.ToValue(), serialize.Value),
                    entity,
                    info.RecipientFilter
                ));
                Owner._pendingHasRecipientFilters |= info.RecipientFilter != null;
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
        public NetworkReplicationSystem Owner;

        public void Execute(float delta, DataStore store, int entity, in NetworkComponent net)
        {
            Uuid entityUuid = store.GetUuid(entity);
            Span<IDataComponent> present = store.Get(entity);

            //  `store.Get` boxes per entity; this path is join-time-only (one-shot per joining client),
            //  so the boxing is bounded by joins, not by the per-tick hot path.
            foreach (NetworkComponentInfo info in Owner._serverOwnedComponents)
            {
                if (info.ReplicationFilter != null && !info.ReplicationFilter(store, entity))
                {
                    continue;
                }

                if (!Contains(present, info.Type))
                {
                    continue;
                }

                Result<byte[]> serialize = info.Codec.Serialize(store, entity);
                if (serialize.Success)
                {
                    Owner._fullSyncPending.Add(new PendingSnapshot(
                        new ComponentSnapshot(entityUuid.ToValue(), info.Uuid.ToValue(), serialize.Value),
                        entity,
                        info.RecipientFilter
                    ));
                    Owner._fullSyncHasRecipientFilters |= info.RecipientFilter != null;
                }
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

    private readonly record struct PendingSnapshot(
        ComponentSnapshot Snapshot,
        int Entity,
        Func<DataStore, int, ReplicationRecipient, bool>? RecipientFilter
    );

    private readonly record struct PendingRemoval(
        ComponentRemoval Removal,
        int Entity,
        Func<DataStore, int, ReplicationRecipient, bool>? RecipientFilter
    );
}

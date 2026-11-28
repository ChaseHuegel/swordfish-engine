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
    private readonly List<PendingDespawn> _removed = [];

    private readonly List<ComponentSnapshot> _filteredComponents = [];
    private readonly List<ComponentRemoval> _filteredRemovals = [];
    private readonly List<ulong> _filteredRemoved = [];

    //  Publish buffers. A WorldSnapshot is serialized synchronously inside the publish loop and then
    //  dropped, so the arrays can be reused across publishes when the counts hold steady.
    private ComponentSnapshot[] _pendingSnapshot = [];
    private ComponentSnapshot[] _filteredComponentSnapshot = [];
    private ComponentRemoval[] _removalSnapshot = [];
    private ComponentRemoval[] _filteredRemovalSnapshot = [];
    private ulong[] _removedSnapshot = [];
    private ulong[] _filteredRemovedSnapshot = [];

    private bool _pendingHasScopedRecipients;
    private bool _pendingRemovalsHasScopedRecipients;
    private bool _fullSyncHasScopedRecipients;
    private bool _removedHasScopedRecipients;

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

    //  Viewer-scoped replication state: tracks which connected clients are subscribed to inspect
    //  non-owned entities (such as server-managed block-entities), queues one-shot initial snapshots
    //  on attach, and emits targeted entity despawns on detach or entity destruction.
    private readonly Dictionary<int, HashSet<Uuid>> _viewersByEntity = [];
    private readonly Dictionary<Uuid, HashSet<int>> _entitiesByViewer = [];
    private readonly Dictionary<Uuid, List<int>> _pendingViewerAttach = [];
    private readonly Dictionary<Uuid, List<ulong>> _pendingViewerDespawns = [];
    private readonly Dictionary<ViewerSubscription, ulong> _deliveredViewerUuids = [];
    private readonly List<ComponentSnapshot> _attachScratch = [];
    private readonly List<ulong> _despawnScratch = [];
    private readonly List<Uuid> _staleViewerClients = [];
    private int _activeViewerSubscriptionCount;

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
    /// captured at the moment ownership is released. When <paramref name="excludedRecipientEntity"/> is
    /// non-null, the client owning that entity uuid does not receive the despawn entry.
    /// </summary>
    public void RequestDespawn(ulong entityUuid, Uuid excludedRecipientEntity = default)
    {
        _removed.Add(new PendingDespawn(entityUuid, excludedRecipientEntity));
        _removedHasScopedRecipients |= excludedRecipientEntity != Uuid.Null;
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
    /// Also tears down any active viewer subscriptions held by <paramref name="clientId"/>.
    /// </summary>
    public void CancelStream(Uuid clientId)
    {
        _streamingClients.Remove(clientId);
        _streamEnds.Remove(clientId);
        _fullSync.Remove(clientId);
        ClearClientViewers(clientId);
    }

    /// <summary>Total number of active viewer-to-entity subscriptions across this world.</summary>
    public int ActiveViewerSubscriptionCount => _activeViewerSubscriptionCount;

    /// <summary>
    /// Subscribes <paramref name="clientId"/> to receive viewer-scoped components for <paramref name="entity"/>
    /// and queues an initial snapshot of its server-owned state on the next publish.
    /// </summary>
    public bool SubscribeViewer(Uuid clientId, int entity)
    {
        if (clientId == Uuid.Null || entity == Entity.Null)
        {
            return false;
        }

        if (!_viewersByEntity.TryGetValue(entity, out HashSet<Uuid>? viewers))
        {
            viewers = [];
            _viewersByEntity[entity] = viewers;
        }

        if (!viewers.Add(clientId))
        {
            return false;
        }

        if (!_entitiesByViewer.TryGetValue(clientId, out HashSet<int>? entities))
        {
            entities = [];
            _entitiesByViewer[clientId] = entities;
        }

        entities.Add(entity);
        _activeViewerSubscriptionCount++;

        if (!_pendingViewerAttach.TryGetValue(clientId, out List<int>? pending))
        {
            pending = [];
            _pendingViewerAttach[clientId] = pending;
        }

        if (!pending.Contains(entity))
        {
            pending.Add(entity);
        }

        return true;
    }

    /// <summary>
    /// Unsubscribes <paramref name="clientId"/> from <paramref name="entity"/> and queues a targeted
    /// despawn for <paramref name="entityUuid"/> when the viewer previously received that entity.
    /// </summary>
    public bool UnsubscribeViewer(Uuid clientId, int entity, ulong entityUuid)
    {
        if (!_viewersByEntity.TryGetValue(entity, out HashSet<Uuid>? viewers) || !viewers.Remove(clientId))
        {
            return false;
        }

        if (viewers.Count == 0)
        {
            _viewersByEntity.Remove(entity);
        }

        if (_entitiesByViewer.TryGetValue(clientId, out HashSet<int>? entities))
        {
            entities.Remove(entity);
            if (entities.Count == 0)
            {
                _entitiesByViewer.Remove(clientId);
            }
        }

        _activeViewerSubscriptionCount = Math.Max(0, _activeViewerSubscriptionCount - 1);
        RemovePendingAttach(clientId, entity);

        if (_deliveredViewerUuids.Remove(new ViewerSubscription(clientId, entity), out ulong deliveredUuid))
        {
            ulong despawnUuid = deliveredUuid != 0UL ? deliveredUuid : entityUuid;
            if (despawnUuid != 0UL)
            {
                EnqueueViewerDespawn(clientId, despawnUuid);
            }
        }

        return true;
    }

    /// <summary>
    /// Detaches every viewer subscribed to <paramref name="entity"/> and queues a targeted despawn for
    /// each viewer that had already received <paramref name="entityUuid"/>. Non-viewers receive nothing.
    /// </summary>
    public void DetachAllViewers(int entity, ulong entityUuid)
    {
        if (!_viewersByEntity.Remove(entity, out HashSet<Uuid>? viewers))
        {
            return;
        }

        foreach (Uuid clientId in viewers)
        {
            if (_entitiesByViewer.TryGetValue(clientId, out HashSet<int>? entities))
            {
                entities.Remove(entity);
                if (entities.Count == 0)
                {
                    _entitiesByViewer.Remove(clientId);
                }
            }

            _activeViewerSubscriptionCount = Math.Max(0, _activeViewerSubscriptionCount - 1);
            RemovePendingAttach(clientId, entity);

            if (_deliveredViewerUuids.Remove(new ViewerSubscription(clientId, entity), out ulong deliveredUuid))
            {
                ulong despawnUuid = deliveredUuid != 0UL ? deliveredUuid : entityUuid;
                if (despawnUuid != 0UL)
                {
                    EnqueueViewerDespawn(clientId, despawnUuid);
                }
            }
        }
    }

    /// <summary>Detaches <paramref name="clientId"/> from every subscribed entity, queuing targeted despawns.</summary>
    public void DetachClientViewers(Uuid clientId, DataStore store)
    {
        if (!_entitiesByViewer.TryGetValue(clientId, out HashSet<int>? entities) || entities.Count == 0)
        {
            return;
        }

        //  Copy to a scratch list because UnsubscribeViewer mutates _entitiesByViewer.
        var snapshot = new List<int>(entities.Count);
        foreach (int entity in entities)
        {
            snapshot.Add(entity);
        }

        for (var i = 0; i < snapshot.Count; i++)
        {
            int entity = snapshot[i];
            ulong entityUuid = store.GetUuid(entity).ToValue();
            UnsubscribeViewer(clientId, entity, entityUuid);
        }
    }

    /// <summary>Returns whether <paramref name="clientId"/> is subscribed to view <paramref name="entity"/>.</summary>
    public bool IsViewerSubscribed(int entity, Uuid clientId)
    {
        return clientId != Uuid.Null
            && _viewersByEntity.TryGetValue(entity, out HashSet<Uuid>? viewers)
            && viewers.Contains(clientId);
    }

    /// <summary>Returns the number of viewers currently subscribed to <paramref name="entity"/>.</summary>
    public int GetViewerCount(int entity)
    {
        return _viewersByEntity.TryGetValue(entity, out HashSet<Uuid>? viewers) ? viewers.Count : 0;
    }

    /// <summary>Copies all active viewer subscriptions into <paramref name="destination"/> without allocating.</summary>
    internal void CopyActiveSubscriptions(List<(Uuid ClientId, int Entity)> destination)
    {
        destination.Clear();
        if (_activeViewerSubscriptionCount == 0)
        {
            return;
        }

        foreach ((int entity, HashSet<Uuid> viewers) in _viewersByEntity)
        {
            foreach (Uuid clientId in viewers)
            {
                destination.Add((clientId, entity));
            }
        }
    }

    /// <summary>Clears all viewer subscriptions without emitting despawns (used on world unload).</summary>
    public void ClearAllViewers()
    {
        _viewersByEntity.Clear();
        _entitiesByViewer.Clear();
        _pendingViewerAttach.Clear();
        _pendingViewerDespawns.Clear();
        _deliveredViewerUuids.Clear();
        _activeViewerSubscriptionCount = 0;
    }

    private void ClearClientViewers(Uuid clientId)
    {
        if (_entitiesByViewer.Remove(clientId, out HashSet<int>? entities))
        {
            foreach (int entity in entities)
            {
                if (_viewersByEntity.TryGetValue(entity, out HashSet<Uuid>? viewers) && viewers.Remove(clientId))
                {
                    if (viewers.Count == 0)
                    {
                        _viewersByEntity.Remove(entity);
                    }

                    _activeViewerSubscriptionCount = Math.Max(0, _activeViewerSubscriptionCount - 1);
                }

                _deliveredViewerUuids.Remove(new ViewerSubscription(clientId, entity));
            }
        }

        _pendingViewerAttach.Remove(clientId);
        _pendingViewerDespawns.Remove(clientId);
    }

    private void RemovePendingAttach(Uuid clientId, int entity)
    {
        if (!_pendingViewerAttach.TryGetValue(clientId, out List<int>? pending))
        {
            return;
        }

        pending.Remove(entity);
        if (pending.Count == 0)
        {
            _pendingViewerAttach.Remove(clientId);
        }
    }

    private void EnqueueViewerDespawn(Uuid clientId, ulong entityUuid)
    {
        if (!_pendingViewerDespawns.TryGetValue(clientId, out List<ulong>? despawns))
        {
            despawns = [];
            _pendingViewerDespawns[clientId] = despawns;
        }

        if (!despawns.Contains(entityUuid))
        {
            despawns.Add(entityUuid);
        }
    }

    private bool HasViewerOrOwner(int entity)
    {
        return (_viewersByEntity.TryGetValue(entity, out HashSet<Uuid>? viewers) && viewers.Count > 0)
            || _sessions.TryGetClient(entity, out _);
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
        _pendingHasScopedRecipients = false;
        _pendingRemovalsHasScopedRecipients = false;

        OnTickAction onTick = new() { Owner = this };
        store.Query<NetworkComponent, OnTickAction>(delta, ref onTick);

        if (_pending.Count == 0
            && _pendingRemovals.Count == 0
            && _removed.Count == 0
            && _fullSync.Count == 0
            && _pendingViewerAttach.Count == 0
            && _pendingViewerDespawns.Count == 0)
        {
            return;
        }

        ComponentSnapshot[]? sharedComponents = _pendingHasScopedRecipients
            ? null
            : CopySnapshots(_pending, ref _pendingSnapshot);
        ComponentRemoval[]? sharedRemovals = _pendingRemovalsHasScopedRecipients
            ? null
            : CopyRemovals(_pendingRemovals, ref _removalSnapshot);
        ulong[]? sharedRemoved = _removedHasScopedRecipients
            ? null
            : CopyDespawns(_removed, ref _removedSnapshot);

        ComponentSnapshot[]? sharedFullState = null;
        if (_fullSync.Count > 0)
        {
            //  Read-only snapshot of every server-owned component on every networked entity, regardless
            //  of dirty, for clients that joined after those components were last published.
            _fullSyncPending.Clear();
            _fullSyncHasScopedRecipients = false;
            CollectFullStateAction collect = new() { Owner = this };
            store.Query<NetworkComponent, CollectFullStateAction>(delta, ref collect);
            if (!_fullSyncHasScopedRecipients)
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

            Recipient recipient = ResolveRecipient(store, clientId, out uint lastProcessedInput);

            ComponentRemoval[] clientRemovals = sharedRemovals
                ?? FilterRemovals(_pendingRemovals, in recipient);
            ulong[] clientRemoved = sharedRemoved
                ?? FilterDespawns(_removed, in recipient);

            ComponentSnapshot[] clientComponents = isFullSync
                ? sharedFullState ?? FilterFullSyncSnapshots(_fullSyncPending, in recipient)
                : sharedComponents ?? FilterDeltaSnapshots(_pending, in recipient);

            if (isFullSync && _entitiesByViewer.TryGetValue(clientId, out HashSet<int>? fullSyncViewed))
            {
                foreach (int viewedEntity in fullSyncViewed)
                {
                    ulong viewedUuid = store.GetUuid(viewedEntity).ToValue();
                    if (viewedUuid != 0UL)
                    {
                        _deliveredViewerUuids[new ViewerSubscription(clientId, viewedEntity)] = viewedUuid;
                    }
                }
            }

            List<int>? attachEntities = null;
            if (_pendingViewerAttach.Remove(clientId, out attachEntities) && attachEntities.Count > 0)
            {
                clientComponents = AppendViewerAttachSnapshots(store, in recipient, clientComponents, attachEntities);
            }

            if (_pendingViewerDespawns.Remove(clientId, out List<ulong>? targetedDespawns) && targetedDespawns.Count > 0)
            {
                clientRemoved = AppendTargetedDespawns(store, clientRemoved, targetedDespawns, attachEntities);
            }

            if (!isFullSync
                && clientComponents.Length == 0
                && clientRemovals.Length == 0
                && clientRemoved.Length == 0)
            {
                continue;
            }

            var snapshot = new WorldSnapshot
            {
                TickNumber = SimTick,
                LastProcessedInput = lastProcessedInput,
                Components = clientComponents,
                RemovedEntities = clientRemoved,
                RemovedComponents = clientRemovals,
            };

            if (!SendSnapshot(peer, snapshot))
            {
                _logger.LogError("Failed to send a snapshot to client {clientId}; closing the peer.", clientId);
                peer.Rpc.Close();
            }
        }

        //  Drop full-sync and viewer requests whose client disconnected before the publish and release scratch payloads.
        PruneDisconnectedViewerQueues();
        _pending.Clear();
        _pendingRemovals.Clear();
        _fullSync.Clear();
        _fullSyncPending.Clear();
        _removed.Clear();
        _removedHasScopedRecipients = false;
        _filteredComponents.Clear();
        _filteredRemovals.Clear();
        _filteredRemoved.Clear();
        _attachScratch.Clear();
        _despawnScratch.Clear();
    }

    private ComponentSnapshot[] AppendViewerAttachSnapshots(
        DataStore store,
        in Recipient recipient,
        ComponentSnapshot[] existing,
        List<int> attachEntities
    )
    {
        _attachScratch.Clear();
        for (var i = 0; i < existing.Length; i++)
        {
            _attachScratch.Add(existing[i]);
        }

        for (var i = 0; i < attachEntities.Count; i++)
        {
            int attachEntity = attachEntities[i];
            if (!IsViewerSubscribed(attachEntity, recipient.ClientId))
            {
                continue;
            }

            Uuid entityUuid = store.GetUuid(attachEntity);
            if (entityUuid == Uuid.Null)
            {
                continue;
            }

            Uuid excludedRecipient = store.TryGet(attachEntity, out NetworkComponent net)
                ? net.ExcludedRecipientEntity
                : Uuid.Null;
            ulong rawUuid = entityUuid.ToValue();
            var deliveredAny = false;

            for (var c = 0; c < _serverOwnedComponents.Length; c++)
            {
                NetworkComponentInfo info = _serverOwnedComponents[c];
                if (!AcceptsRecipient(attachEntity, excludedRecipient, info.Audience, in recipient))
                {
                    continue;
                }

                if (!store.Has(info.Type, attachEntity))
                {
                    continue;
                }

                ulong typeUuid = info.Uuid.ToValue();
                if (ContainsSnapshot(_attachScratch, rawUuid, typeUuid))
                {
                    deliveredAny = true;
                    continue;
                }

                Result<byte[]> serialize = info.Codec.Serialize(store, attachEntity);
                if (!serialize.Success)
                {
                    continue;
                }

                _attachScratch.Add(new ComponentSnapshot(rawUuid, typeUuid, serialize.Value));
                deliveredAny = true;
            }

            if (deliveredAny)
            {
                _deliveredViewerUuids[new ViewerSubscription(recipient.ClientId, attachEntity)] = rawUuid;
            }
        }

        if (_attachScratch.Count == existing.Length)
        {
            return existing;
        }

        ComponentSnapshot[] merged = _attachScratch.ToArray();
        _attachScratch.Clear();
        return merged;
    }

    private ulong[] AppendTargetedDespawns(
        DataStore store,
        ulong[] existing,
        List<ulong> targeted,
        List<int>? attachedThisTick
    )
    {
        _despawnScratch.Clear();
        for (var i = 0; i < existing.Length; i++)
        {
            _despawnScratch.Add(existing[i]);
        }

        for (var i = 0; i < targeted.Count; i++)
        {
            ulong entityUuid = targeted[i];
            if (entityUuid == 0UL || _despawnScratch.Contains(entityUuid))
            {
                continue;
            }

            if (IsAttachedUuid(store, attachedThisTick, entityUuid))
            {
                continue;
            }

            _despawnScratch.Add(entityUuid);
        }

        if (_despawnScratch.Count == existing.Length)
        {
            return existing;
        }

        ulong[] merged = _despawnScratch.ToArray();
        _despawnScratch.Clear();
        return merged;
    }

    private static bool IsAttachedUuid(DataStore store, List<int>? attachedThisTick, ulong entityUuid)
    {
        if (attachedThisTick == null)
        {
            return false;
        }

        for (var i = 0; i < attachedThisTick.Count; i++)
        {
            if (store.GetUuid(attachedThisTick[i]).ToValue() == entityUuid)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsSnapshot(List<ComponentSnapshot> snapshots, ulong entityUuid, ulong typeUuid)
    {
        for (var i = 0; i < snapshots.Count; i++)
        {
            ComponentSnapshot snapshot = snapshots[i];
            if (snapshot.Entity == entityUuid && snapshot.TypeUuid == typeUuid)
            {
                return true;
            }
        }

        return false;
    }

    private void PruneDisconnectedViewerQueues()
    {
        if (_pendingViewerAttach.Count > 0)
        {
            _staleViewerClients.Clear();
            foreach (Uuid clientId in _pendingViewerAttach.Keys)
            {
                if (!_sessions.TryGetEntity(clientId, out _))
                {
                    _staleViewerClients.Add(clientId);
                }
            }

            for (var i = 0; i < _staleViewerClients.Count; i++)
            {
                _pendingViewerAttach.Remove(_staleViewerClients[i]);
            }
        }

        if (_pendingViewerDespawns.Count > 0)
        {
            _staleViewerClients.Clear();
            foreach (Uuid clientId in _pendingViewerDespawns.Keys)
            {
                if (!_sessions.TryGetEntity(clientId, out _))
                {
                    _staleViewerClients.Add(clientId);
                }
            }

            for (var i = 0; i < _staleViewerClients.Count; i++)
            {
                _pendingViewerDespawns.Remove(_staleViewerClients[i]);
            }
        }
    }

    private Recipient ResolveRecipient(DataStore store, Uuid clientId, out uint lastProcessedInput)
    {
        lastProcessedInput = 0;
        if (!_sessions.TryGetEntity(clientId, out int sessionEntity) || sessionEntity == Entity.Null)
        {
            return new Recipient(clientId, Entity.Null, Uuid.Null);
        }

        Uuid sessionUuid = store.GetUuid(sessionEntity);
        if (sessionUuid == Uuid.Null)
        {
            return new Recipient(clientId, Entity.Null, Uuid.Null);
        }

        if (store.TryGet(sessionEntity, out NetworkComponent net))
        {
            lastProcessedInput = net.LastAckedInput;
        }

        return new Recipient(clientId, sessionEntity, sessionUuid);
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

    private ComponentSnapshot[] FilterDeltaSnapshots(List<PendingSnapshot> source, in Recipient recipient)
    {
        PopulateFilteredSnapshots(source, in recipient);
        return ToExactArray(_filteredComponents, ref _filteredComponentSnapshot);
    }

    private ComponentSnapshot[] FilterFullSyncSnapshots(List<PendingSnapshot> source, in Recipient recipient)
    {
        PopulateFilteredSnapshots(source, in recipient);
        ComponentSnapshot[] copy = _filteredComponents.Count == 0 ? [] : _filteredComponents.ToArray();
        _filteredComponents.Clear();
        return copy;
    }

    private void PopulateFilteredSnapshots(List<PendingSnapshot> source, in Recipient recipient)
    {
        _filteredComponents.Clear();
        for (var i = 0; i < source.Count; i++)
        {
            PendingSnapshot entry = source[i];
            if (AcceptsRecipient(entry.Entity, entry.ExcludedRecipientEntity, entry.Audience, in recipient))
            {
                _filteredComponents.Add(entry.Snapshot);
            }
        }
    }

    private ComponentRemoval[] FilterRemovals(List<PendingRemoval> source, in Recipient recipient)
    {
        _filteredRemovals.Clear();
        for (var i = 0; i < source.Count; i++)
        {
            PendingRemoval entry = source[i];
            if (AcceptsRecipient(entry.Entity, entry.ExcludedRecipientEntity, entry.Audience, in recipient))
            {
                _filteredRemovals.Add(entry.Removal);
            }
        }

        return ToExactArray(_filteredRemovals, ref _filteredRemovalSnapshot);
    }

    private ulong[] FilterDespawns(List<PendingDespawn> source, in Recipient recipient)
    {
        _filteredRemoved.Clear();
        for (var i = 0; i < source.Count; i++)
        {
            PendingDespawn entry = source[i];
            if (entry.ExcludedRecipientEntity == Uuid.Null || entry.ExcludedRecipientEntity != recipient.EntityUuid)
            {
                _filteredRemoved.Add(entry.EntityUuid);
            }
        }

        return ToExactArray(_filteredRemoved, ref _filteredRemovedSnapshot);
    }

    private bool AcceptsRecipient(
        int entity,
        Uuid excludedRecipientEntity,
        ReplicationAudience audience,
        in Recipient recipient
    ) {
        if (excludedRecipientEntity != Uuid.Null && excludedRecipientEntity == recipient.EntityUuid)
        {
            return false;
        }

        return audience switch
        {
            ReplicationAudience.OwnerOnly => recipient.Entity != Entity.Null && entity == recipient.Entity,
            ReplicationAudience.OthersOnly => entity != recipient.Entity,
            ReplicationAudience.ViewersOnly => (recipient.Entity != Entity.Null && entity == recipient.Entity)
                || IsViewerSubscribed(entity, recipient.ClientId),
            _ => true,
        };
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

    private static ulong[] CopyDespawns(List<PendingDespawn> source, ref ulong[] cache)
    {
        if (source.Count == 0)
        {
            return cache.Length == 0 ? cache : cache = [];
        }

        if (cache.Length != source.Count)
        {
            cache = new ulong[source.Count];
        }

        for (var i = 0; i < source.Count; i++)
        {
            cache[i] = source[i].EntityUuid;
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
            && inventoryEvent.Transfer != null)
        {
            var stage = new StageTransferInventory(inventoryEvent.SequenceNumber, inventoryEvent.Transfer.Value);
            store.Query<NetworkComponent, StageTransferInventory>(sessionEntity, 0f, ref stage);
        }

        if (info.Type == typeof(StationActionEvent)
            && store.TryGet(sessionEntity, out StationActionEvent stationEvent)
            && stationEvent.Action != null)
        {
            StationActionOp op = stationEvent.Action.Value;
            if (op.IsWithinWireBounds())
            {
                var stage = new StageStationAction(stationEvent.SequenceNumber, in op);
                store.Query<NetworkComponent, StageStationAction>(sessionEntity, 0f, ref stage);
            }
            else
            {
                store.AddOrUpdate(sessionEntity, new StationActionEvent());
                _logger.LogDebug(
                    "Dropping out-of-bounds StationActionEvent {sequence} for entity {entity}.",
                    stationEvent.SequenceNumber, sessionEntity);
            }
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

    private struct StageTransferInventory(uint sequenceNumber, in TransferOp transfer) : IForEach<NetworkComponent>
    {
        private readonly TransferOp _transfer = transfer;

        public void Execute(float delta, DataStore store, int entity, in NetworkComponent net)
        {
            net.StagedInventoryOps?.Stage(sequenceNumber, in _transfer);
        }
    }

    private struct StageStationAction(uint sequenceNumber, in StationActionOp op) : IForEach<NetworkComponent>
    {
        private readonly StationActionOp _op = op;

        public void Execute(float delta, DataStore store, int entity, in NetworkComponent net)
        {
            net.StagedStationActions?.Stage(sequenceNumber, in _op);
        }
    }

    private struct OnTickAction : IForEach<NetworkComponent>
    {
        public NetworkReplicationSystem Owner;

        public void Execute(float delta, DataStore store, int entity, in NetworkComponent net)
        {
            Uuid entityUuid = store.GetUuid(entity);
            Uuid excludedRecipient = net.ExcludedRecipientEntity;

            foreach (NetworkComponentInfo info in Owner._serverOwnedComponents)
            {
                if (!store.IsDirty(info.Type, entity))
                {
                    continue;
                }

                if (info.Audience == ReplicationAudience.ViewersOnly && !Owner.HasViewerOrOwner(entity))
                {
                    store.ClearDirty(info.Type, entity);
                    continue;
                }

                bool hasScopedRecipients = excludedRecipient != Uuid.Null || info.Audience != ReplicationAudience.All;

                //  Removal publishes as a ComponentRemoval when the component no longer exists; the
                //  dirty flag is cleared for both outcomes, and never both in one tick.
                if (!store.Has(info.Type, entity))
                {
                    store.ClearDirty(info.Type, entity);
                    Owner._pendingRemovals.Add(new PendingRemoval(
                        new ComponentRemoval(entityUuid.ToValue(), info.Uuid.ToValue()),
                        entity,
                        excludedRecipient,
                        info.Audience
                    ));
                    Owner._pendingRemovalsHasScopedRecipients |= hasScopedRecipients;
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
                    excludedRecipient,
                    info.Audience
                ));
                Owner._pendingHasScopedRecipients |= hasScopedRecipients;
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
            Uuid excludedRecipient = net.ExcludedRecipientEntity;
            Span<IDataComponent> present = store.Get(entity);

            //  `store.Get` boxes per entity; this path is join-time-only (one-shot per joining client),
            //  so the boxing is bounded by joins, not by the per-tick hot path.
            foreach (NetworkComponentInfo info in Owner._serverOwnedComponents)
            {
                if (info.Audience == ReplicationAudience.ViewersOnly && !Owner.HasViewerOrOwner(entity))
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
                        excludedRecipient,
                        info.Audience
                    ));
                    Owner._fullSyncHasScopedRecipients |= excludedRecipient != Uuid.Null || info.Audience != ReplicationAudience.All;
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

    private readonly record struct Recipient(Uuid ClientId, int Entity, Uuid EntityUuid);

    private readonly record struct ViewerSubscription(Uuid ClientId, int Entity);

    private readonly record struct PendingSnapshot(
        ComponentSnapshot Snapshot,
        int Entity,
        Uuid ExcludedRecipientEntity,
        ReplicationAudience Audience
    );

    private readonly record struct PendingRemoval(
        ComponentRemoval Removal,
        int Entity,
        Uuid ExcludedRecipientEntity,
        ReplicationAudience Audience
    );

    private readonly record struct PendingDespawn(
        ulong EntityUuid,
        Uuid ExcludedRecipientEntity
    );
}

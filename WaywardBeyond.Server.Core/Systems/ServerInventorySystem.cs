using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Swordfish.ECS;
using WaywardBeyond.Shared.Gameplay;
using WaywardBeyond.Shared.Networking.Components;
using WaywardBeyond.Shared.Networking.Transport;

namespace WaywardBeyond.Server.Core.Systems;

/// <summary>
/// Server-side consumption of inbound inventory operations. Runs between the replication ApplyStage and
/// interaction processing, so a move and a place in the same tick resolve against the moved inventory:
/// each staged op is validated and applied to the authoritative <see cref="InventoryComponent"/> via the
/// shared resolver, and the component is marked dirty so its echo flows downstream and corrects any
/// client prediction. Ops are sequence-deduped by <see cref="InventoryOpStageBuffer"/>; the server
/// tracks the last consumed sequence per entity.
/// </summary>
public sealed class ServerInventorySystem : IServerWorldSystem
{
    private readonly ILogger<ServerInventorySystem> _logger;

    private readonly Dictionary<int, uint> _lastConsumedSequences = [];

    public ServerInventorySystem(in ILogger<ServerInventorySystem> logger)
    {
        _logger = logger;
    }

    public void Tick(float delta, DataStore store)
    {
        ConsumeOpsAction action = new() { Owner = this };
        store.Query<NetworkComponent, InventoryComponent, ConsumeOpsAction>(delta, ref action);
    }

    private void Consume(DataStore store, int entity, in NetworkComponent net)
    {
        InventoryOpStageBuffer? staged = net.StagedInventoryOps;
        if (staged == null)
        {
            return;
        }

        if (!_lastConsumedSequences.TryGetValue(entity, out uint lastSequence))
        {
            lastSequence = 0;
        }

        bool consumed = false;
        while (staged.TryConsume(lastSequence, out InventoryOpStageBuffer.InventoryOp op))
        {
            lastSequence = op.SequenceNumber;
            consumed = true;

            if (!store.TryGet(entity, out InventoryComponent inventory))
            {
                _logger.LogWarning("Dropping inventory op {sequence} for entity {entity} without an inventory.", op.SequenceNumber, entity);
                continue;
            }

            InventoryComponent updated = inventory;
            if (SharedInventoryResolver.Apply(ref updated, op.SlotMove))
            {
                store.AddOrUpdate(entity, updated);
                store.MarkDirty<InventoryComponent>(entity);
            }
        }

        if (consumed)
        {
            _lastConsumedSequences[entity] = lastSequence;

            NetworkComponent updated = net;
            updated.StagedInventoryOps = null;
            store.AddOrUpdate(entity, updated);
        }
    }

    private struct ConsumeOpsAction : IForEach<NetworkComponent, InventoryComponent>
    {
        public ServerInventorySystem Owner;

        public void Execute(float delta, DataStore store, int entity, in NetworkComponent net, in InventoryComponent inventory)
        {
            Owner.Consume(store, entity, net);
        }
    }
}
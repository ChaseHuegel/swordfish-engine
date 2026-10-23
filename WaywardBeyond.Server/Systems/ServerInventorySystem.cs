using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Gameplay.Interactions;
using WaywardBeyond.Gameplay.Networking;

namespace WaywardBeyond.Server.Systems;

/// <summary>
/// Server-side consumption of inbound inventory operations. Runs between the replication ApplyStage and
/// interaction processing, so a move and a place in the same tick resolve against the moved inventory:
/// each staged op is validated and applied to the authoritative <see cref="InventoryComponent"/> via the
/// shared resolver, and the component is marked dirty so its echo flows downstream and corrects any
/// client prediction. Ops are sequence-deduped by the staged ring; the server tracks the last consumed
/// sequence per entity.
/// </summary>
public sealed class ServerInventorySystem(ILogger<ServerInventorySystem> logger) : IServerWorldSystem
{
    private readonly Dictionary<int, uint> _lastConsumedSequences = [];

    public void Tick(float delta, DataStore store)
    {
        ConsumeOpsAction action = new() { Owner = this };
        store.Query<NetworkComponent, InventoryComponent, ConsumeOpsAction>(delta, ref action);
    }

    private void Consume(DataStore store, int entity, in NetworkComponent net)
    {
        StageRing<InventoryOp>? staged = net.StagedInventoryOps;
        if (staged == null)
        {
            return;
        }

        if (!_lastConsumedSequences.TryGetValue(entity, out uint lastSequence))
        {
            lastSequence = 0;
        }

        bool consumed = false;
        Result<InventoryOp> stagedOp = staged.Consume(lastSequence);
        while (stagedOp.Success)
        {
            InventoryOp op = stagedOp.Value;
            lastSequence = op.SequenceNumber;
            consumed = true;

            if (!store.TryGet(entity, out InventoryComponent inventory))
            {
                logger.LogWarning("Dropping inventory op {sequence} for entity {entity} without an inventory.", op.SequenceNumber, entity);
            }
            else
            {
                InventoryComponent updated = inventory;
                if (InventoryResolver.Apply(ref updated, op.SlotMove))
                {
                    store.AddOrUpdate(entity, updated);
                }
            }

            stagedOp = staged.Consume(lastSequence);
        }

        if (consumed)
        {
            _lastConsumedSequences[entity] = lastSequence;
            staged.Clear();
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
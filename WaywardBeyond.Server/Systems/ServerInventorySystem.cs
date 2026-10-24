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
/// shared resolver, and the component is marked dirty when an op changed it so its echo flows downstream
/// and corrects any client prediction. Ops are sequence-deduped by the staged ring; the watermark lives on
/// the session's <see cref="NetworkComponent"/>, so a reused mirror starts at zero.
/// </summary>
public sealed class ServerInventorySystem : IServerWorldSystem
{
    /// <inheritdoc/>
    public void Tick(float delta, DataStore store)
    {
        ConsumeOpsAction action = new() { Owner = this };
        store.QueryRef<NetworkComponent, InventoryComponent, ConsumeOpsAction>(delta, ref action);
    }

    private void Consume(
        DataStore store,
        int entity,
        in NetworkComponent net,
        in InventoryComponent inventory,
        out uint consumedSequence
    ) {
        consumedSequence = net.LastConsumedInventorySequence;

        StageRing<InventoryOp>? staged = net.StagedInventoryOps;
        if (staged == null)
        {
            return;
        }

        InventoryComponent updated = inventory;
        var changed = false;
        var consumed = false;

        Result<InventoryOp> stagedOp = staged.Consume(consumedSequence);
        while (stagedOp.Success)
        {
            InventoryOp op = stagedOp.Value;
            consumedSequence = op.SequenceNumber;
            consumed = true;

            //  An invalid op is a no-op, but its sequence is still consumed so it cannot replay.
            changed |= InventoryResolver.Apply(ref updated, op.SlotMove);
            stagedOp = staged.Consume(consumedSequence);
        }

        if (!consumed)
        {
            return;
        }

        //  Change-gated: an unchanged inventory would still be marked dirty and re-published.
        if (changed)
        {
            store.AddOrUpdate(entity, updated);
        }

        staged.Clear();
    }

    private struct ConsumeOpsAction : IForEachRef<NetworkComponent, InventoryComponent>
    {
        public ServerInventorySystem Owner;

        public void Execute(float delta, DataStore store, int entity, ref Ref<NetworkComponent> net, ref Ref<InventoryComponent> inventory)
        {
            ref readonly NetworkComponent current = ref net.Read;
            ref readonly InventoryComponent inventoryValue = ref inventory.Read;

            Owner.Consume(store, entity, in current, in inventoryValue, out uint consumedSequence);

            //  Change-gated: a write of an unchanged value still marks the component dirty.
            if (consumedSequence != current.LastConsumedInventorySequence)
            {
                net.Write.LastConsumedInventorySequence = consumedSequence;
            }
        }
    }
}

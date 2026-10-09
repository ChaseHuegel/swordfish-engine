using Swordfish.ECS;
using WaywardBeyond.Networking.Components;

namespace WaywardBeyond.Client.Components;

/// <summary>
/// Client-side bookkeeping for the local player's inventory moves: <see cref="Outbound"/> buffers the
/// discrete move ops not yet transmitted upstream, sequence-stamped for retransmit dedupe. Unlike the
/// inventory-UI mutations themselves (which predict directly onto the local copy), the buffer retains
/// every op so rapid moves between replication sends are not silently dropped.
/// </summary>
public struct PendingInventoryComponent : IDataComponent
{
    /// <summary>Ops authored by the inventory UI awaiting replication to the server.</summary>
    public readonly InventoryOpStageBuffer Outbound;

    /// <summary>Monotonic op sequence counter; also used by <see cref="InventoryEvent.SequenceNumber"/>.</summary>
    public uint NextSequence;

    public PendingInventoryComponent()
    {
        Outbound = new InventoryOpStageBuffer();
        NextSequence = 0;
    }
}
using Krill;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Data.Networking;
using WaywardBeyond.Gameplay.Networking;

namespace WaywardBeyond.Client.Components;

/// <summary>
/// Client-side bookkeeping for the local player's voxel interactions: <see cref="Queue"/> tracks the
/// outstanding predictions awaiting their authoritative <see cref="VoxelEditMessage"/> echo (see
/// <see cref="PendingInteractionQueue"/>), and <see cref="Outbound"/> buffers the discrete interaction
/// edges not yet transmitted upstream. Unlike a single <c>InteractionEvent</c> component slot, the
/// outbound buffer retains every click, so rapid placements that land between replication sends are not
/// silently overwritten and dropped.
/// </summary>
public struct PendingInteractionComponent(in PendingInteractionQueue queue) : IDataComponent
{
    private const int CAPACITY = 64;

    public readonly PendingInteractionQueue Queue = queue;

    /// <summary>Edges authored by <c>PlayerInteractionService</c> awaiting replication to the server.</summary>
    public readonly StageRing<InteractionEvent> Outbound = new(CAPACITY);
}
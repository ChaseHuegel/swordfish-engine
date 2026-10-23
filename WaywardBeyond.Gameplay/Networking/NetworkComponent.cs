using Swordfish.ECS;
using WaywardBeyond.Data.Components;

namespace WaywardBeyond.Gameplay.Networking;

/// <summary>The per-client networking state bound to a session entity.</summary>
public struct NetworkComponent : IDataComponent
{
    private const int INPUT_CAPACITY = 64;
    private const int INTERACTION_CAPACITY = 64;
    private const int INVENTORY_CAPACITY = 32;

    /// <summary>The gameplay session the entity is bound to.</summary>
    public Session Session;

    /// <summary>The newest input sim tick the client has sent.</summary>
    public uint LastAckedInput;

    /// <summary>The newest interaction sequence consumed for this session.</summary>
    public uint LastConsumedInteractionSequence;

    /// <summary>The newest inventory op sequence consumed for this session.</summary>
    public uint LastConsumedInventorySequence;

    /// <summary>Sim-tick-keyed inbound command staging, populated server-side.</summary>
    public StageRing<InputComponent>? StagedInputs;

    /// <summary>Sim-tick-keyed inbound interaction staging, populated server-side.</summary>
    public StageRing<InteractionEvent>? StagedInteractions;

    /// <summary>Sequence-keyed inbound inventory staging, populated server-side.</summary>
    public StageRing<InventoryOp>? StagedInventoryOps;

    /// <summary>Creates the state for one connected client. The consumed sequence watermarks start at zero.</summary>
    public static NetworkComponent Create()
    {
        return new NetworkComponent
        {
            StagedInputs = new StageRing<InputComponent>(INPUT_CAPACITY),
            StagedInteractions = new StageRing<InteractionEvent>(INTERACTION_CAPACITY),
            StagedInventoryOps = new StageRing<InventoryOp>(INVENTORY_CAPACITY),
        };
    }
}

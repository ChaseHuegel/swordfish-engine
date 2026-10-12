using Swordfish.ECS;
using WaywardBeyond.Networking.Sessions;

namespace WaywardBeyond.Networking.Components;

/// <summary>
/// The per-client networking state bound to a session entity: the owning <see cref="Session"/>, the
/// client's acknowledged input/snapshot ticks, and the server-side inbound staging buffers.
/// </summary>
public struct NetworkComponent : IDataComponent
{
    public Session Session;
    public uint LastAckedInput;
    public uint LastAckedSnapshot;
    public uint ServerTPS;

    /// <summary>Sim-tick-keyed inbound command staging, populated server-side.</summary>
    public InputStageBuffer? StagedInputs;

    /// <summary>Sim-tick-keyed inbound interaction-edge staging, populated server-side.</summary>
    public InteractionStageBuffer? StagedInteractions;

    /// <summary>Sequence-keyed inbound inventory-op staging, populated server-side.</summary>
    public InventoryOpStageBuffer? StagedInventoryOps;
}

using Swordfish.ECS;
using WaywardBeyond.Shared.Networking.Sessions;

namespace WaywardBeyond.Shared.Networking.Components;

public struct NetworkComponent : IDataComponent
{
    public Session Session;
    public uint LastAckedInput;
    public uint LastAckedSnapshot;
    public uint ServerTPS;

    /// <summary>Sim-tick-keyed inbound command staging, populated server-side.</summary>
    public InputStageBuffer? StagedInputs;
}

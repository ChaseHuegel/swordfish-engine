using Swordfish.ECS;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Gameplay.Networking;

namespace WaywardBeyond.Client.Components;

/// <summary>The local player's outbound input samples awaiting server acknowledgment.</summary>
public struct PendingInputComponent : IDataComponent
{
    private const int CAPACITY = 128;

    /// <summary>The staged input samples, oldest first.</summary>
    public StageRing<InputComponent>? Ring;

    /// <summary>Creates the ring for the local player.</summary>
    public static PendingInputComponent Create()
    {
        return new PendingInputComponent { Ring = new StageRing<InputComponent>(CAPACITY) };
    }
}

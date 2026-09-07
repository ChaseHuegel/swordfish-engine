using Swordfish.ECS;
using WaywardBeyond.Client.Core.Networking;

namespace WaywardBeyond.Client.Core.Systems;

/// <summary>
/// Polls the client <see cref="WorldsClient"/> for in-flight world-management responses (save listing,
/// create/delete world requests) on the ECS thread, so the menu can await them without either blocking a
/// thread or racing the transport. This system runs regardless of <see cref="GameState"/> (the ECS thread
/// ticks in the menu too), which is what lets the save-listing UI be served entirely from the server.
/// </summary>
internal sealed class ClientWorldServiceSystem : IEntitySystem
{
    private readonly WorldsClient _worlds;

    public ClientWorldServiceSystem(in WorldsClient worlds)
    {
        _worlds = worlds;
    }

    public void Tick(float delta, DataStore store)
    {
        _worlds.Poll();
    }
}
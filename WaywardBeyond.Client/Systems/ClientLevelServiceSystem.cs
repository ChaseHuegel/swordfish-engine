using Swordfish.ECS;
using WaywardBeyond.Client.Networking;

namespace WaywardBeyond.Client.Systems;

/// <summary>
/// Polls the client <see cref="LevelsClient"/> for in-flight level-management responses (save listing,
/// create/delete level requests) on the ECS thread, so the menu can await them without either blocking a
/// thread or racing the transport. This system runs regardless of <see cref="GameState"/> (the ECS thread
/// ticks in the menu too), which is what lets the save-listing UI be served entirely from the server.
/// </summary>
internal sealed class ClientLevelServiceSystem : IEntitySystem
{
    private readonly LevelsClient _levels;

    public ClientLevelServiceSystem(in LevelsClient levels)
    {
        _levels = levels;
    }

    public void Tick(float delta, DataStore store)
    {
        _levels.Poll();
    }
}
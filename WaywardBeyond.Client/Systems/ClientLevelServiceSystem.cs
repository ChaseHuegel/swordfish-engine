using Swordfish.ECS;
using WaywardBeyond.Client.Networking;

namespace WaywardBeyond.Client.Systems;

/// <summary>
/// Ticks the client RPC host on the ECS thread. It enforces pending call deadlines, so a menu request
/// or a join never waits forever. Runs in every game state, including the menu.
/// </summary>
internal sealed class ClientLevelServiceSystem : IEntitySystem
{
    private readonly ClientRpcHost _host;

    public ClientLevelServiceSystem(in ClientRpcHost host)
    {
        _host = host;
    }

    public void Tick(float delta, DataStore store)
    {
        _host.Poll();
    }
}

using Shoal.DependencyInjection;
using WaywardBeyond.Networking.Transport;
using WaywardBeyond.Server;

namespace WaywardBeyond.Client.Networking;

/// <summary>
/// Seeds the in-process host's <see cref="LocalConnection"/> loopback into <see cref="PendingJoins"/>
/// at startup. The loopback then rides the ordinary join route: it waits in the pending set until its
/// <c>JoinRequest</c> binds it to the world it joins.
/// </summary>
internal sealed class LocalLoopbackSeed : IAutoActivate
{
    public LocalLoopbackSeed(PendingJoins pendingJoins, LocalConnection localConnection)
    {
        pendingJoins.Add(localConnection.Server);
    }
}
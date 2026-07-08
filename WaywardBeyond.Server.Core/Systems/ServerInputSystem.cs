using Microsoft.Extensions.Logging;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Components;
using WaywardBeyond.Shared.Networking.Snapshots;
using WaywardBeyond.Shared.Networking.Transport;

namespace WaywardBeyond.Server.Core.Systems;

public sealed class ServerInputSystem : IEntitySystem
{
    private readonly INetworkTransport _transport;
    private readonly ILogger<ServerInputSystem> _logger;

    public ServerInputSystem(
        in INetworkTransport transport,
        in ILogger<ServerInputSystem> logger
    ) {
        _transport = transport;
        _logger = logger;
    }

    public void Tick(float delta, DataStore store)
    {
        if (_transport.IsLocal)
        {
            return;
        }

        while (_transport.TryReceive<ClientInputMsg>(out ClientInputMsg msg))
        {
            if (!store.Find<NetworkComponent>(
                    (NetworkComponent net) => net.NetworkID == msg.ClientID,
                    out int entity))
            {
                _logger.LogWarning("Received input for unknown network ID {id}.", msg.ClientID);
                continue;
            }

            InputComponent input = msg.ToComponent();
            store.AddOrUpdate(entity, input);

            store.Query<NetworkComponent>(entity, 0f, (float d, DataStore s, int e, ref NetworkComponent net) =>
            {
                net.LastAckedInput = msg.SequenceNumber;
                s.AddOrUpdate(e, net);
            });
        }
    }
}

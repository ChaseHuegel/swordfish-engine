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

        Result<ClientInputMsg> receiveResult;
        while ((receiveResult = _transport.Receive<ClientInputMsg>()).Success)
        {
            ClientInputMsg msg = receiveResult.Value;

            if (!store.TryGet(Uuid.FromValue(msg.ClientUuid), out int entity))
            {
                _logger.LogWarning("Received input for unknown uuid {uuid}.", msg.ClientUuid);
                continue;
            }

            InputComponent input = msg.ToComponent();
            store.AddOrUpdate(entity, input);

            ApplyInputAction applyInput = new()
            {
                Owner = this,
                LastAckedInput = msg.SequenceNumber,
                LastAckedSnapshot = msg.ServerTickAtSample,
            };
            store.QueryRef<NetworkComponent, ApplyInputAction>(entity, 0f, ref applyInput);
        }
    }

    private struct ApplyInputAction : IForEachRef<NetworkComponent>
    {
        public ServerInputSystem Owner;
        public uint LastAckedInput;
        public uint LastAckedSnapshot;

        public void Execute(float delta, DataStore store, int entity, ref Ref<NetworkComponent> net)
        {
            net.Write.LastAckedInput = LastAckedInput;
            net.Write.LastAckedSnapshot = LastAckedSnapshot;
        }
    }
}

using System;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Serialization;
using WaywardBeyond.Shared.Networking.Transport;
using Swordfish.Library.Util;
using Xunit;

namespace Swordfish.Tests;

public class LocalConnectionTests
{
    private static LocalConnection CreateConnection()
    {
        return new LocalConnection(new INetworkSerializer[] { new NsdMessageSerializer<WorldSnapshot>() });
    }

    [Fact]
    public void ClientToServerRoundTrip()
    {
        LocalConnection connection = CreateConnection();
        var sent = new WorldSnapshot
        {
            TickNumber = 10,
            LastProcessedInput = 4,
            Components = [new ComponentSnapshot(0x111, 0x222, [1, 2, 3])],
            RemovedEntities = [0x333],
        };

        connection.Client.Send(sent);
        Result<WorldSnapshot> received = connection.Server.Receive<WorldSnapshot>();

        Assert.True(received.Success);
        Assert.Equal(sent.TickNumber, received.Value.TickNumber);
        Assert.Equal(sent.LastProcessedInput, received.Value.LastProcessedInput);
        Assert.Equal(sent.Components[0].Entity, received.Value.Components[0].Entity);
        Assert.Equal(sent.Components[0].Payload, received.Value.Components[0].Payload);
        Assert.Equal(sent.RemovedEntities, received.Value.RemovedEntities);
    }

    [Fact]
    public void ServerToClientRoundTrip()
    {
        LocalConnection connection = CreateConnection();
        var sent = new WorldSnapshot { TickNumber = 7, LastProcessedInput = 1, Components = [], RemovedEntities = [] };

        connection.Server.Send(sent);
        Result<WorldSnapshot> received = connection.Client.Receive<WorldSnapshot>();

        Assert.True(received.Success);
        Assert.Equal(sent.TickNumber, received.Value.TickNumber);
    }

    [Fact]
    public void EmptyReceiveFails()
    {
        LocalConnection connection = CreateConnection();
        Assert.False(connection.Server.Receive<WorldSnapshot>().Success);
    }
}
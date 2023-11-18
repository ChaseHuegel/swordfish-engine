using System.Net;
using Needlefish;
using Swordfish.Library.Networking;
using Swordfish.Networking;
using Swordfish.Networking.Serialization;
using Swordfish.Networking.UDP;
using Xunit;
using Xunit.Abstractions;

namespace Swordfish.Tests;

public class NetTests : TestBase
{
    public NetTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public async Task SendUdpPacket()
    {
        var tcs = new TaskCompletionSource();

        var unicastProvider = new UnicastProvider(1234);
        var needlefishSerializer = new NeedlefishSerializer<PacketHeader>();
        var packetFilter = new PacketFilter();
        var server = new MessageService<PacketHeader, IPEndPoint>(unicastProvider, unicastProvider, needlefishSerializer, packetFilter);
        server.Start();
        server.Received += OnServerReceived;

        unicastProvider = new UnicastProvider();
        needlefishSerializer = new NeedlefishSerializer<PacketHeader>();
        packetFilter = new PacketFilter();
        var client = new MessageService<PacketHeader, IPEndPoint>(unicastProvider, unicastProvider, needlefishSerializer, packetFilter);
        client.Start();
        client.Received += OnClientReceived;
        client.Send(new PacketHeader { PacketID = 10 }, IPEndPoint.Parse("127.0.0.1:1234"));

        void OnServerReceived(object? sender, PacketHeader packet)
        {
            Assert.Equal(10, packet.PacketID);
            tcs.SetResult();
        }

        void OnClientReceived(object? sender, PacketHeader packet)
        {
            //  Do nothing
        }

        await tcs.Task;
    }

    [Fact]
    public void SerializedPacketDoesDeserialize()
    {
        Handshake.BeginPacket packet = new Handshake.BeginPacket
        {
            Secret = "test"
        };

        byte[] buffer = NeedlefishFormatter.Serialize(packet);
        Handshake.BeginPacket deserializedPacket = (Handshake.BeginPacket)NeedlefishFormatter.Deserialize(typeof(Handshake.BeginPacket), buffer);

        Assert.Equal(packet.Secret, deserializedPacket.Secret);
    }
}

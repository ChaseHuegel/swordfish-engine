using System.Net;
using Needlefish;
using Swordfish.Library.Networking;
using Swordfish.Networking;
using Swordfish.Networking.Serialization;
using Swordfish.Networking.UDP;
using Xunit;
using Xunit.Abstractions;
using Packet = Swordfish.Networking.Packet;

namespace Swordfish.Tests;

public class NetTests : TestBase
{
    private class TestPacket
    {
        public readonly string? Content;

        public TestPacket()
        {
            Content = null;
        }

        public TestPacket(string content)
        {
            Content = content;
        }
    }

    public NetTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public async Task SendUdpPacket()
    {
        var tcs = new TaskCompletionSource();

        var readerWriter = new UnicastProvider(1234);
        var serializer = new PacketSerializer();
        var filter = new PacketFilter();
        var server = new MessageService<Packet, IPEndPoint>(readerWriter, readerWriter, serializer, filter);
        server.Start();
        server.Received += OnServerReceived;

        readerWriter = new UnicastProvider();
        serializer = new PacketSerializer();
        filter = new PacketFilter();
        var client = new MessageService<Packet, IPEndPoint>(readerWriter, readerWriter, serializer, filter);
        client.Start();
        client.Received += OnClientReceived;

        for (int i = 0; i < 20; i++)
        {
            var packet = serializer.Serialize(new TestPacket("Hello world!"));
            client.Send(packet, IPEndPoint.Parse("127.0.0.1:1234"));
        }

        void OnServerReceived(object? sender, Packet packet)
        {
            TestPacket testPacket = NeedlefishFormatter.Deserialize<TestPacket>(packet.Payload);
            if (packet.Sequence == 19)
                tcs.SetResult();
        }

        void OnClientReceived(object? sender, Packet packet)
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

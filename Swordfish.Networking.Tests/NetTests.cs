using System.Diagnostics;
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
    private class TestPacket : IPacketDefinition
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

        public ushort GetPacketID()
        {
            return 100;
        }
    }

    public NetTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact(Timeout = 10000)]
    public async Task SendReceiveUdpPacket()
    {
        var tcs = new TaskCompletionSource();
        int received = 0;
        List<ushort> receivedSequences = new List<ushort>();

        var readerWriter = new UnicastProvider(1234);
        var serializer = new PacketSerializer();
        var filter = new PacketFilter<IPEndPoint>();
        var server = new PacketService<IPEndPoint>(readerWriter, readerWriter, serializer, filter);
        server.Start();
        server.Received += OnServerReceived;

        readerWriter = new UnicastProvider();
        serializer = new PacketSerializer();
        filter = new PacketFilter<IPEndPoint>();
        var client = new PacketService<IPEndPoint>(readerWriter, readerWriter, serializer, filter);
        client.Start();
        client.Received += OnClientReceived;

        Stopwatch overallTime = Stopwatch.StartNew();
        Stopwatch sendTime = Stopwatch.StartNew();

        const int PACKET_COUNT = 500;
        for (int i = 0; i < PACKET_COUNT; i++)
        {
            client.Send(new TestPacket("Hello world!"), IPEndPoint.Parse("127.0.0.1:1234"));
        }
        sendTime.Stop();
        Stopwatch sendDoneToReceiveDone = Stopwatch.StartNew();

        void OnServerReceived(object? sender, Packet packet)
        {
            received++;
            TestPacket testPacket = NeedlefishFormatter.Deserialize<TestPacket>(packet.Payload);
            receivedSequences.Add(packet.Sequence);
            Output.WriteLine($"Received #{received}: ID {packet.ID}, Sequence: {packet.Sequence}, Content: {testPacket.Content}");
            if (received == PACKET_COUNT)
                tcs.SetResult();
        }

        void OnClientReceived(object? sender, Packet packet)
        {
            //  Do nothing
        }

        await tcs.Task;
        overallTime.Stop();
        sendDoneToReceiveDone.Stop();
        Output.WriteLine($"Elapsed: {overallTime.ElapsedMilliseconds / 1000f}s");
        Output.WriteLine($"Time to send: {sendTime.ElapsedMilliseconds / 1000f}s");
        Output.WriteLine($"Time between complete send and final receive: {sendDoneToReceiveDone.ElapsedMilliseconds / 1000f}s");
        Assert.True(false);
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

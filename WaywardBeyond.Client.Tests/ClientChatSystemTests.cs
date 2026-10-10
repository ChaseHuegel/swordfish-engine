using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Systems;
using WaywardBeyond.Config;
using WaywardBeyond.Networking;
using WaywardBeyond.Networking.Serialization;
using WaywardBeyond.Networking.Transport;

namespace WaywardBeyond.Client.Tests;

[TestFixture]
public class ClientChatSystemTests
{
    [Test]
    public void DrainsInboundIntoHistoryAndFlushesOutboundToTransport()
    {
        var connection = new LocalConnection(new INetworkSerializer[]
        {
            new NsdMessageSerializer<ChatMessage>(),
        });
        var service = new ChatService(new ChatConfig());
        var system = new ClientChatSystem(connection.Client, service);

        connection.Server.Send(new ChatMessage { CharacterId = 1, SenderName = "Alice", Value = "Hi" });
        system.Tick(delta: 0f, new DataStore());

        ChatLine[] snapshot = service.Snapshot();
        Assert.That(snapshot, Has.Length.EqualTo(1));
        Assert.That(snapshot[0].Message.Value, Is.EqualTo("Hi"));
        Assert.That(snapshot[0].ReceivedAt, Is.GreaterThan(0));

        service.EnqueueSend("Hello");
        system.Tick(delta: 0f, new DataStore());

        Result<ChatMessage> sent = connection.Server.Receive<ChatMessage>();
        Assert.That(sent.Success, Is.True);
        Assert.That(sent.Value.Value, Is.EqualTo("Hello"));
    }
}
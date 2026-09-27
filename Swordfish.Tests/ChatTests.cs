using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Server.Core;
using WaywardBeyond.Server.Core.Components;
using WaywardBeyond.Server.Core.Systems;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Serialization;
using WaywardBeyond.Shared.Networking.Sessions;
using WaywardBeyond.Shared.Networking.Transport;
using Xunit;

namespace Swordfish.Tests;

/// <summary>
/// Covers the chat wire message shape and the server-authoritative relay: identity stamping from the
/// player mirror, sanitization, per-client delivery, and the drop rule for unjoined clients.
/// </summary>
public class ChatTests
{
    [Fact]
    public void ChatMessageRoundTrips()
    {
        var message = new ChatMessage { CharacterId = 42, SenderName = "Alice", Value = "Hello, world!" };
        var serializer = new NsdMessageSerializer<ChatMessage>();

        ChatMessage decoded = serializer.Deserialize(serializer.Serialize(message));

        Assert.Equal(message.CharacterId, decoded.CharacterId);
        Assert.Equal(message.SenderName, decoded.SenderName);
        Assert.Equal(message.Value, decoded.Value);
    }

    [Fact]
    public void RelaysStampedSanitizedMessageToEveryClient()
    {
        var hub = new ServerConnectionHub();
        var connection = new LocalConnection(new INetworkSerializer[]
        {
            new NsdMessageSerializer<ChatMessage>(),
        });
        Uuid clientId = hub.Add(connection.Server);

        var store = new DataStore();
        var sessions = new SessionManager();
        SeatPlayer(store, sessions, clientId, characterId: 1234, name: "Alice", sessionId: 1);

        var system = new ServerChatSystem(hub, sessions, NullLogger<ServerChatSystem>.Instance);

        connection.Client.Send(new ChatMessage
        {
            CharacterId = 999,
            SenderName = "Spoof",
            Value = new string('x', 1000) + "\n\r\0tail",
        });

        system.Tick(delta: 0f, store);

        Result<ChatMessage> relay = connection.Client.Receive<ChatMessage>();
        Assert.True(relay.Success);
        Assert.Equal((ulong)1234, relay.Value.CharacterId);
        Assert.Equal("Alice", relay.Value.SenderName);
        Assert.Equal(ServerChatSystem.MAX_MESSAGE_LENGTH, relay.Value.Value.Length);
        Assert.DoesNotContain('\n', relay.Value.Value);
        Assert.DoesNotContain('\0', relay.Value.Value);

        Assert.False(connection.Client.Receive<ChatMessage>().Success);
    }

    [Fact]
    public void RelaysToAllConnectedClients()
    {
        var hub = new ServerConnectionHub();
        var first = new LocalConnection(new INetworkSerializer[]
        {
            new NsdMessageSerializer<ChatMessage>(),
        });
        var second = new LocalConnection(new INetworkSerializer[]
        {
            new NsdMessageSerializer<ChatMessage>(),
        });
        Uuid firstId = hub.Add(first.Server);
        Uuid secondId = hub.Add(second.Server);

        var store = new DataStore();
        var sessions = new SessionManager();
        SeatPlayer(store, sessions, firstId, characterId: 1, name: "One", sessionId: 1);
        SeatPlayer(store, sessions, secondId, characterId: 2, name: "Two", sessionId: 2);

        var system = new ServerChatSystem(hub, sessions, NullLogger<ServerChatSystem>.Instance);
        first.Client.Send(new ChatMessage { Value = "Hi all" });

        system.Tick(delta: 0f, store);

        Result<ChatMessage> echo = first.Client.Receive<ChatMessage>();
        Result<ChatMessage> broadcast = second.Client.Receive<ChatMessage>();
        Assert.True(echo.Success);
        Assert.True(broadcast.Success);
        Assert.Equal("One", echo.Value.SenderName);
        Assert.Equal("One", broadcast.Value.SenderName);
    }

    [Fact]
    public void DropsMessagesFromUnjoinedClients()
    {
        var hub = new ServerConnectionHub();
        var connection = new LocalConnection(new INetworkSerializer[]
        {
            new NsdMessageSerializer<ChatMessage>(),
        });
        hub.Add(connection.Server);

        var system = new ServerChatSystem(hub, new SessionManager(), NullLogger<ServerChatSystem>.Instance);
        connection.Client.Send(new ChatMessage { Value = "Hi" });

        system.Tick(delta: 0f, new DataStore());

        Assert.False(connection.Client.Receive<ChatMessage>().Success);
    }

    private static void SeatPlayer(DataStore store, SessionManager sessions, Uuid clientId, ulong characterId, string name, uint sessionId)
    {
        int entity = store.Alloc();
        store.AddOrUpdate(entity, new OwnedCharacterComponent(characterId));
        store.AddOrUpdate(entity, new IdentifierComponent(name));
        sessions.Register(store, entity, clientId, new Session(sessionId));
    }
}
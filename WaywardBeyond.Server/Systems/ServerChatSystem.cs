using System.Text;
using Microsoft.Extensions.Logging;
using Swordfish.ECS;
using WaywardBeyond.Data.Networking;
using WaywardBeyond.Server.Components;

namespace WaywardBeyond.Server.Systems;

/// <summary>
/// Server-authoritative chat relay. Inbound <see cref="ChatMessage"/>s from any client are stamped with
/// the sender's authoritative identity from the player mirror (<see cref="OwnedCharacterComponent"/> for
/// the character id, <see cref="IdentifierComponent"/> for the display name), sanitized, logged to the
/// standard engine log, and broadcast to every peer in the world through its cached client proxy.
/// Identity is never trusted from the wire; a client can only supply text.
/// </summary>
public sealed class ServerChatSystem(
    ServerPeers peers,
    SessionManager sessions,
    ILogger<ServerChatSystem> logger
) : IServerWorldSystem
{
    /// <summary>Maximum characters of a chat message after sanitization.</summary>
    private const int MAX_MESSAGE_LENGTH = 512;

    /// <inheritdoc/>
    public void Tick(float delta, DataStore store)
    {
        foreach (Uuid clientId in sessions.Clients)
        {
            if (!peers.TryGet(clientId, out ServerPeer peer))
            {
                continue;
            }

            while (peer.Chats.TryDequeue(out ChatMessage message))
            {
                Relay(store, clientId, message);
            }
        }
    }

    private void Relay(DataStore store, Uuid clientId, in ChatMessage message)
    {
        //  Only joined players may speak; drop everything else silently.
        if (!sessions.TryGetEntity(clientId, out int entity) || !store.TryGet(entity, out OwnedCharacterComponent character))
        {
            return;
        }

        string senderName = store.TryGet(entity, out IdentifierComponent identifier) ? identifier.Name ?? string.Empty : string.Empty;

        var relay = new ChatMessage
        {
            CharacterUuid = store.GetUuid(entity).ToValue(),
            SenderName = senderName,
            Value = Sanitize(message.Value),
        };

        logger.LogInformation("[Chat] {CharacterUuid} {SenderName}: {Value}", relay.CharacterUuid, relay.SenderName, relay.Value);

        foreach (Uuid recipientId in sessions.Clients)
        {
            if (peers.TryGet(recipientId, out ServerPeer peer))
            {
                peer.Client.ChatRelay(relay);
            }
        }
    }

    private static string Sanitize(in string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (char character in value)
        {
            //  Control characters (including newlines) are stripped so a chat line stays single-line and safe to log.
            if (char.IsControl(character))
            {
                continue;
            }

            builder.Append(character);
            if (builder.Length == MAX_MESSAGE_LENGTH)
            {
                break;
            }
        }

        return builder.ToString();
    }
}

using System.Text;
using Microsoft.Extensions.Logging;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Server.Components;
using WaywardBeyond.Networking;
using WaywardBeyond.Networking.Transport;

namespace WaywardBeyond.Server.Systems;

/// <summary>
/// Server-authoritative chat relay. Inbound <see cref="ChatMessage"/>s from any client are stamped with
/// the sender's authoritative identity from the player mirror (<see cref="OwnedCharacterComponent"/> for
/// the character id, <see cref="IdentifierComponent"/> for the display name), sanitized, logged to the
/// standard engine log, and broadcast to every connected client. Identity is never trusted from the
/// wire; a client can only supply text.
/// </summary>
public sealed class ServerChatSystem : IServerWorldSystem
{
    /// <summary>Maximum characters of a chat message after sanitization.</summary>
    public const int MAX_MESSAGE_LENGTH = 512;

    private readonly ServerConnectionHub _hub;
    private readonly SessionManager _sessions;
    private readonly ILogger _logger;

    public ServerChatSystem(
        in ServerConnectionHub hub,
        SessionManager sessions,
        ILogger<ServerChatSystem> logger
    ) {
        _hub = hub;
        _sessions = sessions;
        _logger = logger;
    }

    public void Tick(float delta, DataStore store)
    {
        foreach ((Uuid clientId, ChatMessage message) in _hub.Receive<ChatMessage>())
        {
            //  Only joined players may speak; drop everything else silently.
            if (!_sessions.TryGetEntity(clientId, out int entity) || !store.TryGet(entity, out OwnedCharacterComponent character))
            {
                continue;
            }

            string senderName = store.TryGet(entity, out IdentifierComponent identifier) ? identifier.Name ?? string.Empty : string.Empty;

            var relay = new ChatMessage
            {
                CharacterUuid = character.CharacterUuid,
                SenderName = senderName,
                Value = Sanitize(message.Value),
            };

            _logger.LogInformation("[Chat] {CharacterUuid} {SenderName}: {Value}", relay.CharacterUuid, relay.SenderName, relay.Value);

            foreach ((Uuid client, _) in _hub.Clients)
            {
                Result send = _hub.Send(client, relay);
                if (!send.Success)
                {
                    _logger.LogWarning("Failed to relay chat message to client {client}: {message}.", client, send.Message);
                }
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
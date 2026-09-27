using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Transport;

namespace WaywardBeyond.Client.Core.Systems;

/// <summary>
/// Pumps chat between the transport and <see cref="ChatService"/> on the ECS thread. Inbound
/// <see cref="ChatMessage"/>s drain into the history buffer; outbound messages queued by the UI are
/// flushed to the server. Keeping transport access on the ECS thread matches the other client systems
/// and avoids cross-thread sends.
/// </summary>
public sealed class ClientChatSystem : IEntitySystem
{
    private readonly IClientConnection _transport;
    private readonly ChatService _chat;

    public ClientChatSystem(
        in IClientConnection transport,
        ChatService chat
    ) {
        _transport = transport;
        _chat = chat;
    }

    public void Tick(float delta, DataStore store)
    {
        Result<ChatMessage> receiveResult;
        while ((receiveResult = _transport.Receive<ChatMessage>()).Success)
        {
            _chat.Add(receiveResult.Value);
        }

        while (_chat.TryDequeueSend(out string value))
        {
            _ = _transport.Send(new ChatMessage { Value = value });
        }
    }
}
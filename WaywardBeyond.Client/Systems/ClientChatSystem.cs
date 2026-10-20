using Swordfish.ECS;
using WaywardBeyond.Client.Networking;
using WaywardBeyond.Networking;

namespace WaywardBeyond.Client.Systems;

/// <summary>
/// Pumps chat between the RPC inbound queue and <see cref="ChatService"/> on the ECS thread. Inbound
/// <see cref="ChatMessage"/>s drain into the history buffer; outbound messages queued by the UI are
/// sent through the cached server proxy.
/// </summary>
internal sealed class ClientChatSystem : IEntitySystem
{
    private readonly ClientRpcHost _host;
    private readonly ClientInbound _inbound;
    private readonly ChatService _chat;

    public ClientChatSystem(
        in ClientRpcHost host,
        in ClientInbound inbound,
        ChatService chat
    ) {
        _host = host;
        _inbound = inbound;
        _chat = chat;
    }

    public void Tick(float delta, DataStore store)
    {
        while (_inbound.Chats.TryDequeue(out ChatMessage message))
        {
            _chat.Add(message);
        }

        while (_chat.TryDequeueSend(out string value))
        {
            _host.Api?.Chat(new ChatMessage { Value = value });
        }
    }
}

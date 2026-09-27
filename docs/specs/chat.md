# Chat

One subject: chat end to end. This covers the wire message, the server relay
and its log output, the client service, and the chat UI. The wire shape
complements [networking-messages](networking-messages.md).

## Wire message

`ChatMessage` lives in
`WaywardBeyond.Shared.Networking/CodeGen/network.nsd`. Fields:
`CharacterId`, `SenderName`, `Value`.

The client sends `Value` only. The server never trusts client-supplied
identity.

Chat messages relay in arrival order. They use their own per-type queue, so
they never compete with replication frames.

## Server relay

`WaywardBeyond.Server.Core/Systems/ServerChatSystem.cs` is ticked in
`ServerContext.Update` after the interaction step and before replication
publish. For each inbound `ChatMessage` it applies these rules:

- Drop the message if the client has no session (it did not join).
- Stamp `CharacterId` from the player mirror's `OwnedCharacterComponent`.
- Stamp `SenderName` from the player mirror's `IdentifierComponent`.
- Strip control characters and truncate `Value` to 512 characters.
- Broadcast the relay to every connected client, including the sender.

## Log output

Every relayed message logs one line through the standard engine log (MEL:
console and `logs/latest.log`):

```
[Chat] {CharacterId} {SenderName}: {Value}
```

The literal `[Chat]` prefix marks chat lines for parsing. Sanitization keeps
the line single-line. `CharacterId` is the character GUID.

The log is plain text. It has no dedicated chat log file.

## Client service

`WaywardBeyond.Client.Core/Systems/ChatService.cs` holds the shared client
chat state. It keeps a bounded ring buffer of received messages (capacity
comes from `MaxHistory`) and a queue of outbound messages.

`ClientChatSystem` runs on the ECS thread. It drains inbound `ChatMessage`s
into the buffer and flushes the outbound queue to the transport. The UI layer
reads the buffer and enqueues sends.

## Config

The chat tunables live in `chat.toml`. See
[config-schemas](config-schemas.md) for the key table.

## Source of truth

- `WaywardBeyond.Shared.Networking/CodeGen/network.nsd`
- `WaywardBeyond.Server.Core/Systems/ServerChatSystem.cs`
- `WaywardBeyond.Server.Core/ServerContext.cs`
- `WaywardBeyond.Client.Core/Systems/ChatService.cs`
- `WaywardBeyond.Client.Core/Systems/ClientChatSystem.cs`
- `WaywardBeyond.Shared.Config/ChatSettings.cs`

## Tests that pin this

- `Swordfish.Tests/ChatTests.cs` covers the round trip, the relay stamping and
  sanitization, the broadcast, and the unjoined-client drop rule.
- `WaywardBeyond.Client.Core.Tests/ChatServiceTests.cs` covers the ring buffer
  bounds and the send queue.
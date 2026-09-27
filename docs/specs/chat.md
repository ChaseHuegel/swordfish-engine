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
comes from `MaxHistory`), stamping each with its arrival time, and a queue of
outbound messages.

`ClientChatSystem` runs on the ECS thread. It drains inbound `ChatMessage`s
into the buffer and flushes the outbound queue to the transport. The UI layer
reads the buffer and enqueues sends.

## Config

The chat tunables live in `chat.toml`. See
[config-schemas](config-schemas.md) for the key table.

## UI

`WaywardBeyond.Client.Core/UI/Layers/ChatLayer.cs` is the chat overlay. It is
an `IUILayer` registered by the client module.

One box renders in both states: a scroll viewport over the send box's
reserved slot. Opening or closing changes only the send box's presence and
the scroll behavior; the box never shifts.

The Enter key opens the chat box. Escape closes it. While the box is open,
gameplay interaction is blocked through `InteractionState` (the cursor is
free), and the send box stays focused even when the user clicks elsewhere.
Enter with non-empty text sends and closes the box. Escape or Enter with
empty text closes without sending.

The scroll viewport caps at 150 pixels and shrinks to the content, so the
newest messages hug its bottom edge. Closed chat sticks to the newest message
and ignores the wheel. Open chat starts stuck to the newest message; the
wheel breaks the stick to read older messages, and scrolling back to the
bottom re-sticks it. A stuck scroller follows new arrivals.

The fade is per message, not per window. Each message shows at full alpha for
`TimeoutSeconds` after its arrival, then fades over one second. Closed chat
hides a message once its fade completes. Because messages fade in arrival
order, the faded messages are always an oldest prefix, and the overlay
renders nothing once every message has faded. Open chat shows every message
at full alpha. `ChatService` stamps the arrival time when the client receives
the message; the overlay itself never resets a message's clock.

The overlay has no background in either state. The send box has a fixed
height, so it anchors to the overlay bottom even when it is empty.

The chat box focuses programmatically when it opens (`UIBuilder.Focus`). The
Enter key that opens the box is consumed by the box's first frame, so it does
not submit an empty message. Clearing the send box also resets its caret and
selection state; leaving those stale would crash the text box on the next
edit.

## Source of truth

- `WaywardBeyond.Shared.Networking/CodeGen/network.nsd`
- `WaywardBeyond.Server.Core/Systems/ServerChatSystem.cs`
- `WaywardBeyond.Server.Core/ServerContext.cs`
- `WaywardBeyond.Client.Core/Systems/ChatService.cs`
- `WaywardBeyond.Client.Core/Systems/ClientChatSystem.cs`
- `WaywardBeyond.Client.Core/UI/Layers/ChatLayer.cs`
- `WaywardBeyond.Shared.Config/ChatSettings.cs`

## Tests that pin this

- `Swordfish.Tests/ChatTests.cs` covers the round trip, the relay stamping and
  sanitization, the broadcast, and the unjoined-client drop rule.
- `WaywardBeyond.Client.Core.Tests/ChatServiceTests.cs` covers the ring buffer
  bounds and the send queue.
- `WaywardBeyond.Client.Core.Tests/ClientChatSystemTests.cs` covers the inbound
  drain and the outbound flush.
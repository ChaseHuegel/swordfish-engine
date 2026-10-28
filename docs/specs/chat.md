# Chat

One subject: chat end to end. This covers the wire message, the server relay
and its log output, the client service, and the chat UI. The wire shape
complements [networking-messages](networking-messages.md).

## Wire message

`ChatMessage` lives in
`WaywardBeyond.Data/Nsd/network.nsd`. Fields:
`CharacterUuid`, `SenderName`, `Value`.

The client sends `Value` only. The relay `CharacterUuid` and `SenderName` come
from the server's player mirror, never from the message. The mirror's name is
the client-chosen display name, clamped to
`CharacterNames.MAX_LENGTH` at join. The value is markup-tolerant by design.

Chat is a client-to-server RPC notification (method 60) and a server-to-client
notification (method 353). `ClientInbound.Chats` stages inbound relays for the
ECS thread. Chat never competes with replication frames for a shared queue.

## Server relay

`WaywardBeyond.Server/Systems/ServerChatSystem.cs` is a world system ticked
after the interaction step and before replication publish (see
[networking-worlds](networking-worlds.md)). `ServerHandlers.OnChat` stages each
`ChatMessage` on `ServerPeer.Chats`; the world tick drains it. For each message
it applies these rules:

- Drop the message if the client has no session (it did not join).
- Stamp `CharacterUuid` from the player mirror's `OwnedCharacterComponent`.
- Stamp `SenderName` from the player mirror's `IdentifierComponent`.
- Strip control characters and truncate `Value` to 512 characters.
- Broadcast the relay through every world peer's cached `GameClientApiClient`,
  including the sender.

## Log output

Every relayed message logs one line through the standard engine log (MEL:
console and `logs/latest.log`):

```
[Chat] {CharacterUuid} {SenderName}: {Value}
```

The literal `[Chat]` prefix marks chat lines for parsing. Sanitization keeps
the line single-line. `CharacterUuid` is the character uuid.

The log is plain text. It has no dedicated chat log file.

## Client service

`WaywardBeyond.Client/Systems/ChatService.cs` holds the shared client
chat state. It keeps a bounded ring buffer of received messages (capacity
comes from `MaxHistory`), stamping each with its arrival time, and a queue of
outbound messages.

`ClientChatSystem` runs on the ECS thread. It drains
`ClientInbound.Chats` into the buffer and sends the outbound queue through the
cached server proxy. The UI layer reads the buffer and enqueues sends.

## Config

The chat tunables live in `chat.toml`. See
[config-schemas](config-schemas.md) for the key table.

## UI

`WaywardBeyond.Client/UI/Layers/ChatLayer.cs` is the chat overlay. It is
an `IUILayer` registered by the client module.

One fixed-geometry box renders in both states: a 200-pixel scroll viewport at a
fixed offset above the send bar's slot. Opening or closing changes only the
outer chrome and the scroll behavior; the messages never shift.

The Enter key opens the chat box. Escape closes it. While the box is open,
gameplay interaction is blocked through `InteractionState` (the cursor is
free), and the send box stays focused even when the user clicks elsewhere.
Enter with non-empty text sends and closes the box. Escape or Enter with
empty text closes without sending.

Each message wraps to the viewport width. Its full wrapped height counts toward
the scroll extent. The scroll viewport is always 200 pixels tall. When the
content height is shorter than 200 pixels, a positive vertical viewport offset
pins the messages to the bottom edge of the viewport. Closed chat sticks to the
newest message and ignores the wheel. Open chat starts stuck to the newest
message; the wheel breaks the stick to read older messages, and scrolling back
to the bottom re-sticks it. A stuck scroller follows new arrivals.

The fade is per message, not per window. Each message shows at full alpha for
`StaleMs` after its arrival, then fades over one second. Closed chat
hides a message once its fade completes. Because messages fade in arrival
order, the faded messages are always an oldest prefix, and the overlay
renders nothing once every message has faded. Open chat shows every message
at full alpha. `ChatService` stamps the arrival time when the client receives
the message; the overlay itself never resets a message's clock.

The open overlay renders a fixed-height `Widgets.Panel` with `UiTheme.BorderHover`
corner caps, a `SUBSPACE COMMS // LOCAL` header bar with an `[ESC] CLOSE` hint, a
`Widgets.Scrollbar` rail beside the 200-pixel history viewport, and a
send bar (`>` prompt, chromeless `Widgets.TextBox`, and `[ENTER]`
hint). Closed chat renders individual translucent message pills with a 2-pixel
vertical cyan bar anchor on the left edge and `[SenderName]` in
`UiTheme.Accent`.

The chat box focuses programmatically when it opens (`UIBuilder.Focus`). The
Enter key that opens the box is consumed by the box's first frame, so it does
not submit an empty message. Clearing the send box also resets its caret and
selection state; leaving those stale would crash the text box on the next
edit.

## Source of truth

- `WaywardBeyond.Data/Nsd/network.nsd`
- `WaywardBeyond.Server/Systems/ServerChatSystem.cs`
- `WaywardBeyond.Client/Systems/ChatService.cs`
- `WaywardBeyond.Client/Systems/ClientChatSystem.cs`
- `WaywardBeyond.Client/UI/Layers/ChatLayer.cs`
- `WaywardBeyond.Config/ChatConfig.cs`

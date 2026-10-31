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

`WaywardBeyond.Client/UI/Layers/ChatLayer.cs` is the chat overlay layer. It is
an `IUILayer` registered by the client module. The layer owns services,
shortcuts, timers, scroll state, and state assembly. It renders through the
generated `ChatView` markup view.

`ChatView.rml` owns the overlay geometry: the 600x270 panel, the 200-pixel
history viewport at panel-space `(6,32)`, the scrollbar at `(590,32)`, the
send bar at `(6,236)`, and the header and divider. `ChatView.cs` holds the
`ChatViewState` record, the send box `TextBoxState`, rich-text formatting, and
the faded color helpers.

One fixed-geometry box renders in both states. Opening or closing changes only
the outer chrome and the scroll behavior. The messages never shift.

The Enter key opens the chat box. Escape closes it. While the box is open,
gameplay interaction is blocked through `InteractionState` (the cursor is
free), and the send box stays focused even when the user clicks elsewhere.
Enter with non-empty text sends and closes the box. Escape or Enter with
empty text closes without sending.

Each message wraps to the viewport width. Its full wrapped height counts toward
the scroll extent. The layer measures a row from the previous frame's
`ChatMessage_{index}` text layout and adds the row padding. The fallback height
is 16 pixels plus padding. The scroll viewport is always 200 pixels tall. When
the content height is shorter than 200 pixels, a positive vertical viewport
offset pins the messages to the bottom edge of the viewport. Closed chat sticks
to the newest message and ignores the wheel. Open chat starts stuck to the
newest message. The wheel breaks the stick to read older messages, and scrolling
back to the bottom re-sticks it. A stuck scroller follows new arrivals.

The fade is per message, not per window. Each message shows at full alpha for
`StaleMs` after its arrival, then fades over one second. Closed chat hides a
message once its fade completes. Because messages fade in arrival order, the
faded messages are always an oldest prefix, and the overlay renders nothing
once every message has faded. Open chat shows every message at full alpha.
`ChatService` stamps the arrival time when the client receives the message.
The overlay itself never resets a message's clock.

Closed chat is passthrough. The history viewport, rows, and message text ignore
hit-testing. Each closed row draws a translucent `--wb-panel-inner` background
with a 2-pixel vertical `--wb-accent` bar on the left edge. The sender run uses
`UiTheme.AccentBrightRichText` followed by the message rich text.

The open overlay draws a `--wb-panel` surface with `--wb-border-hover` corner
caps, an inset `--wb-border-inner` border, a `SUBSPACE COMMS // LOCAL` header
bar with an `[ESC] CLOSE` hint, a 4-pixel scrollbar rail beside the 200-pixel
history viewport, and a send bar (`>` prompt, chromeless text input, and
`[ENTER]` hint). The styles live in `Components.rss`. An open-only hit area
over the history viewport blocks clicks and wheel input from the HUD.

The chat box focuses programmatically when it opens. A `FocusTarget` directive
sets focus on `TextBox_Chat` each open frame, so a click elsewhere cannot
strand the box unfocused. The Enter key that opens the box is consumed by the
box's first frame, so it does not submit an empty message. Clearing the send
box also resets its caret, selection, modifier, and submit state. Leaving those
stale would crash the text box on the next edit.

## Source of truth

- `WaywardBeyond.Data/Nsd/network.nsd`
- `WaywardBeyond.Server/Systems/ServerChatSystem.cs`
- `WaywardBeyond.Client/Systems/ChatService.cs`
- `WaywardBeyond.Client/Systems/ClientChatSystem.cs`
- `WaywardBeyond.Client/UI/Layers/ChatLayer.cs`
- `WaywardBeyond.Client/UI/Layers/ChatView.rml`
- `WaywardBeyond.Client/UI/Layers/ChatView.cs`
- `WaywardBeyond.Client/UI/Components/Components.rss`
- `WaywardBeyond.Config/ChatConfig.cs`

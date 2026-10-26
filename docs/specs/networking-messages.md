# Networking — Wire Messages

One subject: the serialized message and component shapes that cross the wire.

## Serialization substrate

Wire payloads are nsd schemas compiled by `Needlefish.Generators` into structs
with generated `Serialize()`/`Deserialize(ReadOnlySpan<byte>)`. The generator
runs during compilation and writes no source files. The schemas live in
`WaywardBeyond.Data/Nsd`. See [needlefish-codegen](needlefish-codegen.md).

`NsdRpc` wraps each payload in its own envelope. The transport frame is a
4-byte little-endian length prefix plus the envelope. The envelope carries the
protocol version, message kind, correlation id, method id, payload, and error.
The game does not write the envelope, type tags, per-type queues, or FIFO
request matching. See [networking-rpc](networking-rpc.md).

A payload over 60,000 bytes is split into ordered fragments. See
[fragments](networking-rpc.md#fragments).

## Snapshots

`WaywardBeyond.Data/Nsd/network.nsd`:

```nsd
message ComponentSnapshot
{
    ulong Entity    = 0;
    ulong TypeUuid  = 1;
    byte[] Payload  = 2;
}

message WorldSnapshot
{
    uint TickNumber           = 0;
    uint LastProcessedInput   = 1;
    ComponentSnapshot[] Components     = 2;
    ulong[] RemovedEntities   = 3;
    ComponentRemoval[] RemovedComponents = 4;
}

message ComponentRemoval { ulong Entity; ulong TypeUuid; }

message SnapshotBatch
{
    uint BatchId = 0;
    uint PartIndex = 1;
    uint PartCount = 2;
    byte[] Payload = 3;
}
```

`TickNumber` is the canonical sim tick (physics-step ordinal) at publish, not
the server's per-world replication tick. `LastProcessedInput` is that client's
own `LastAckedInput`. A `WorldSnapshot` over the fragment budget travels as
ordered `SnapshotBatch` parts. The parts carry a serialized `WorldSnapshot`
payload.

## Transform and physics

```nsd
message TransformMessage { float PositionX/Y/Z; float OrientationX/Y/Z/W; float ScaleX/Y/Z; }
message PhysicsMessage  { float VelocityX/Y/Z; float AngularVelocityX/Y/Z; }
```

`PhysicsComponent.Torque` is dual-semantics and was canonicalized: the wire
`PhysicsMessage` carries velocity and angular velocity, well-defined only at
sync boundaries.

## Input component

`InputComponent` is both a networked component and an nsd message
(`WaywardBeyond.Data/Nsd/components.nsd`). It carries absolute,
sensitivity-resolved look totals (radians) plus continuous held state:

```nsd
message InputComponent
{
    float MovementX          = 0;
    float MovementY          = 1;
    float MovementZ          = 2;
    float LookPitch          = 3;
    float LookYaw            = 4;
    float LookRoll           = 5;
    uint  SequenceNumber     = 6;
    uint  ServerTickAtSample = 7;
    bool  PrimaryHeld        = 9;   // continuous held state
    bool  SecondaryHeld      = 10;  // continuous held state
}
```

Mouse sensitivity is a client-local setting, never networked. The wire carries
resolved look, not config.

Look totals are relative to the current session. A join resets them on the
client and forgets the identity's last applied totals on the receiver, so a
same-character rejoin starts from the seeded spawn rather than the previous
session's accumulated look.

## Interaction event (extensible pseudo-union)

Discrete interaction edges ride a `ClientOwned` message as an extensible union
of nullable hint sub-messages. Common edge metadata stays at the root;
per-interaction hints are nullable sub-messages.

`Kind` is the button/edge and is **not** the union discriminator. Hint
presence is. A wholly hint-less event is valid and resolves to `Action.None`.

```nsd
message InteractionEvent
{
    ulong Entity;            // player mirror address (dedupe/routing)
    uint  SequenceNumber;
    uint  ServerTickAtSample;
    byte  Kind;              // PrimaryPressed / PrimaryReleased / SecondaryPressed / SecondaryReleased
    BrickInteraction? Brick; // hint payload; future interactions add their own nullable sub-message
}

message BrickInteraction
{
    ulong TargetEntity = 0;
    int  TargetX, TargetY, TargetZ;  // client hint target cell
    byte HintShape;         // place only
    byte HintOrientation;   // place only
}
```

`InteractionKind` maps the `byte Kind` to the edge. Callers guard optional
hints with `.HasValue` / pattern matching.

Inventory moves use the same envelope pattern with a nullable op member; the
union shapes, op modes, and resolution rules live in
[inventory](networking-inventory.md).

```nsd
message InventoryEvent
{
    ulong Entity;            // player mirror address (dedupe/routing)
    uint  SequenceNumber;
    SlotMoveOp? SlotMove;    // op payload; future ops add their own nullable sub-message
}
```

## Interaction context components

`WaywardBeyond.Data/Nsd/components.nsd` also defines the server-owned
interaction context and the body view:

```nsd
message EquipmentComponent   { int ActiveInventorySlot = 0; }
message InventoryComponent   { WaywardBeyond.Data.ItemData[] Contents = 0; }
message GameModeComponent    { int Value = 0; }
message BodyViewComponent    { string Body = 0; }
message IdentifierMessage    { string Name; string Tag; }
```

`GameModeComponent` carries an `int` on the wire (cross-namespace enums do not
serialize as enums in Needlefish codegen) and exposes `Mode` via the partial.

## Authoritative voxel edits

Downstream broadcast delta in `network.nsd`:

```nsd
message VoxelEditMessage
{
    ulong EntityUuid = 0;
    int X, Y, Z;
    WaywardBeyond.Data.Voxel Voxel;
    uint Sequence = 5;
    string? BrickId = 6;
}
```

The edit is a server-to-client RPC notification (method 350). It rides the
reliable send queue with every other server event.

## Session heartbeats

`ServerHeartbeatMessage` (server → client, notification 354) and
`ClientHeartbeatMessage` (client → server, notification 3) carry the liveness
and lag signal at `NetworkingConfig.Protocol.HeartbeatIntervalMs`. The server
emits to every accepted connection from establishment: the per-world
`ServerHeartbeatService` covers peers bound to a world, and the host-level
`PendingConnectionHeartbeat` covers unbound menu peers. See
[transports](networking-transports.md#heartbeats).

```nsd
message ServerHeartbeatMessage
{
    uint TPS;           // server fixed-step count per wall second, averaged
    uint TickNumber;    // current server sim tick
    uint PlayerCount;   // server-stamped from the world session count (own client included)
}
message ClientHeartbeatMessage
{
    uint TickNumber;             // client sim tick (AlignTo-ed from snapshots)
    uint LastAppliedSnapshotTick;
}
```

The heartbeats replace the transport keepalive frame (see
[transports](networking-transports.md)). Bandwidth is negligible: one small
reliable frame per second per connection.

## Chat

`WaywardBeyond.Data/Nsd/network.nsd`:

```nsd
message ChatMessage
{
    ulong  CharacterUuid = 0;  // server-stamped
    string SenderName  = 1;  // server-stamped
    string Value       = 2;  // client-authored text
}
```

`ChatMessage` is the only chat wire message. The client sends text only. The
server stamps the sender identity from the player mirror and broadcasts the
relay to every client, including the sender. See [chat](chat.md) for the relay
rules, the log line, and the client behavior.

## Join and level-stream messages

`WaywardBeyond.Data/Nsd/levels.nsd`: `ClientHello`, `JoinRequest`,
`JoinAccept`, `LevelStreamComplete`, `CharacterView`, `CharacterState`, and the
save-listing set (`NewLevelRequest`/`Response`, `ListLevels*`,
`DeleteLevel*`, `SaveLevel*`).

The join is two RPC calls. `JoinRequest` returns `JoinAccept`.
`StreamLevelRequest` starts the stream. Each `LevelStreamChunk` carries a
serialized `VoxelEntityData` fragment. `LevelStreamComplete` is the stream
response. The old `LevelEntityAdd` wrapper is gone.

```nsd
message StreamLevelRequest
{
    string LevelGuid = 0;
}

message LevelStreamChunk
{
    uint PartIndex = 0;
    uint PartCount = 1;
    byte[] Payload = 2;
}
```

Every menu call is correlated by nsd-rpc, so request order never matters. See
[join](networking-join.md) and [persistence](persistence.md).

`CharacterState` carries the client's authoritative **initial** character
context, including its saved skill statistics:

```nsd
message CharacterState
{
    ulong CharacterUuid = 0;
    string Name = 1;
    int Body = 2;
    ItemData[]? Inventory = 3;
    int ActiveInventorySlot = 4;
    GameMode GameMode = 5;
    Statistic[]? Statistics = 6;   // skill XP seed; server keeps only what matches a skill id
}
```

`GameMode` resolves as an enum because `levels.nsd` includes `saves.nsd` in the
same namespace. The server rejects an undefined value on a level request and
falls back to adventure on a join seed.

`ClientHello.UserId` is the client's stable user id. The client sends it once
when the connection opens. The server keeps it as `ServerPeer.Claim`.
Menu-time requests resolve permissions from it, and `ServerJoinSystem` binds the
same claim to the session at join. See [permissions](permissions.md).

`ListLevelsResponse.CanCreateSave` tells the client whether the server permits
it to create a save. See [persistence](persistence.md).

## Notifications and skill state

Server-to-client gameplay signaling in `network.nsd`. Servers never localize:
the client resolves keys against its own locale files.

```nsd
message NotificationMessage
{
    NotificationType Type = 0;
    string Key = 1;            // localization key for the primary template
    string[] Args = 2;         // positional args; text values are localization keys
    string? ID = 3;            // Bar only: dedupe id (e.g. skill id)
    float? Amount = 4;         // Bar only: progress 0..1
    string? Subtext = 5;       // Bar only: optional right-aligned label key or literal
    string[]? SubtextArgs = 6; // Bar only: positional args for Subtext
    int? Segments = 7;         // Bar only: optional segment count (null or <= 1 is continuous)
    uint? Color = 8;           // Bar only: optional 0xRRGGBBAA fill color override
}

message SkillStateUpdateMessage
{
    string SkillId = 0;
    long TotalXP = 1;
    int Level = 2;
    long XPIntoLevel = 3;
    long GainedXP = 4;
}
```

`NotificationType` is generated from `network.nsd`
(`WaywardBeyond.Data.Networking.NotificationType`); its values are a wire
contract. `ClientNotificationSystem`
writes each `SkillStateUpdateMessage` total into the client-owned character save.
See [skills](skills.md) for the authority model.

## LAN beacon

```nsd
message LanBeacon { string ServerName; int TcpPort; int ProtocolVersion; int PlayerCount; }
```

A UDP control-plane beacon only. See [transports](networking-transports.md).

## Source of truth

- Schema files:
  `WaywardBeyond.Data/Nsd/{network,components,voxels,levels,saves}.nsd`.
- Generator wiring: `WaywardBeyond.Data/WaywardBeyond.Data.csproj`.
- Generated namespaces: `WaywardBeyond.Data.Networking` for wire messages,
  `WaywardBeyond.Data.Components` for networked components.

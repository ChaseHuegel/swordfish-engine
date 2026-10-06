# Networking — Wire Messages

One subject: the serialized message and component shapes that cross the wire.

## Serialization substrate

Wire messages are nsd schemas compiled by `nsdc` into structs with generated
`Serialize()`/`Deserialize(ReadOnlySpan<byte>)`. The generated `.cs` files under
`**/CodeGen/Output` are auto-generated; never hand-edit them.

`NsdMessageSerializer<T>` (`Serialization/NsdMessageSerializer.cs`) adapts any
nsd message to the generic `ISerializer<T>` used by transports, by
reflection-driving the generated methods. A `SerializerCache` indexes the
DI-provided serializers by message type.

> **No envelope.** The wire is plain serialized nsd messages with raw
> transport-only framing (TCP uses a 4-byte length prefix). There is no
> `GamePacket` envelope, no per-message sequence/ack/RTT layer. Reliability and
> ordering are left to the transport (TCP today).

## Snapshots

`WaywardBeyond.Shared.Networking/CodeGen/network.nsd`:

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
```

`TickNumber` is the canonical sim tick (physics-step ordinal) at publish, not
the server's per-world replication tick. `LastProcessedInput` is that client's
own `LastAckedInput`.

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
(`CodeGen/components.nsd`). It carries absolute, sensitivity-resolved look
totals (radians) plus continuous held state:

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

`CodeGen/components.nsd` also defines the server-owned interaction context and
the body view:

```nsd
message EquipmentComponent   { int ActiveInventorySlot = 0; }
message InventoryComponent   { WaywardBeyond.Shared.Data.ItemData[] Contents = 0; }
message GameModeComponent    { int Value = 0; }
message BodyViewComponent    { int Body = 0; }
message IdentifierMessage    { string Name; string Tag; }
```

`GameModeComponent` carries an `int` on the wire (cross-namespace enums do not
serialize as enums in nsd codegen) and exposes `Mode` via the partial.

## Authoritative voxel edits

Downstream broadcast delta in `network.nsd`:

```nsd
message VoxelEditMessage
{
    ulong EntityUuid = 0;
    int X, Y, Z;
    WaywardBeyond.Shared.Data.Voxel Voxel;
    uint Sequence = 5;
}
```

## Session heartbeats

`ServerHeartbeatMessage` (server → client, reliable, per connection at
`HeartbeatIntervalMs`) and `ClientHeartbeatMessage` (client → server, same
cadence) carry the liveness + lag signal. The server emits to every accepted
connection from establishment: the per-world `ServerHeartbeatService` covers
connections bound to a world hub, and the host-level `ServerHostHeartbeat`
covers connections still in the pending set (the menu and character-creation
window). See [transports](networking-transports.md).

```nsd
message ServerHeartbeatMessage
{
    uint TPS;           // server fixed-step count per wall second, averaged
    uint TickNumber;    // current server sim tick
    uint PlayerCount;   // server-stamped from the hub (own client included)
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

`WaywardBeyond.Shared.Networking/CodeGen/network.nsd`:

```nsd
message ChatMessage
{
    ulong  CharacterId = 0;  // server-stamped
    string SenderName  = 1;  // server-stamped
    string Value       = 2;  // client-authored text
}
```

`ChatMessage` is the only chat wire message. The client sends text only. The
server stamps the sender identity from the player mirror and broadcasts the
relay to every client, including the sender. See [chat](chat.md) for the relay
rules, the log line, and the client behavior.

## Join and level-stream messages

`WaywardBeyond.Shared.Data/CodeGen/levels.nsd`: `JoinRequest`,
`JoinAccept`, `LevelEntityAdd`, `LevelStreamComplete`, `PublicView`,
`CharacterSeed`, and the save-listing set
(`NewLevelRequest`/`Response`, `ListLevels*`, `DeleteLevel*`, `SaveLevel*`).
See [join](networking-join.md) and [persistence](persistence.md).

`CharacterSeed` carries the client's authoritative **initial** character
context, including its saved skill statistics:

```nsd
message CharacterSeed
{
    ulong CharacterId = 0;
    string Name = 1;
    int Body = 2;
    ItemData[]? InventoryContents = 3;
    int ActiveInventorySlot = 4;
    int GameMode = 5;
    Statistic[]? Statistics = 6;   // skill XP seed; server keeps only what matches a skill id
}
```

`JoinRequest.UserId` is the client's stable user id. The server treats it as an
unauthenticated claim and binds it to the session for permission checks. See
[permissions](permissions.md).

## Notifications and skill state

Server-to-client gameplay signaling in `network.nsd`. Servers never localize:
the client resolves keys against its own locale files.

```nsd
message NotificationMessage
{
    byte Type = 0;        // NotificationType: Toast/Action/Interaction/Bar
    string Key = 1;       // localization key for the template
    string[] Args = 2;    // positional args; text values are localization keys
    string? ID = 3;       // Bar only: dedupe id (e.g. skill id)
    float? Amount = 4;    // Bar only: progress 0..1
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

`NotificationType` is a shared enum (`WaywardBeyond.Shared.Networking/
NotificationType.cs`); its byte values are a wire contract. `ClientNotificationSystem`
writes each `SkillStateUpdateMessage` total into the client-owned character save.
See [skills](skills.md) for the authority model.

## LAN beacon

```nsd
message LanBeacon { string ServerName; int TcpPort; int ProtocolVersion; int PlayerCount; }
```

A UDP control-plane beacon only. See [transports](networking-transports.md).

## Source of truth

- Schema files: `WaywardBeyond.Shared.Networking/CodeGen/network.nsd`,
  `.../components.nsd`, `WaywardBeyond.Shared.Data/CodeGen/{voxels,levels,saves}.nsd`.
- `nsdc` build wiring: the Exec targets in each `.csproj`
  (e.g. `WaywardBeyond.Shared.Networking.csproj`, `WaywardBeyond.Shared.Data.csproj`).

## Tests that pin this

- `Swordfish.Tests` codec / round-trip tests for `InteractionEvent`,
  `CharacterSeed`, and the component codecs.
- `WaywardBeyond.Client.Core.Tests` cover voxel-object processing.
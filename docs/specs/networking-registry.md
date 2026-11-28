# Networking — Registry

One subject: stable wire identity for networked components.

## `NetworkRegistry`

`WaywardBeyond.Networking/Registry/NetworkRegistry.cs` maps networked component
types to a stable wire uuid, an owning endpoint, and a payload codec. One
instance is registered as a DI singleton by `NetworkingComposition`; systems take
it through constructor injection.

- `Register<T>(Uuid, NetworkDirection, IPayloadCodec<T>, ReplicationAudience)`
  is the only registration path. The typed codec ties the registration to the
  component at compile time. The optional `audience` parameter declares which
  clients receive a `ServerOwned` component (`All`, `OwnerOnly`, `OthersOnly`,
  or `ViewersOnly`). The method returns `Result` with a contextual failure message
  (duplicate type, duplicate uuid, `Uuid.Null`, or a scoped audience on a
  `ClientOwned` component).
- `Freeze()` closes the registry to registration and caches the per-direction
  views. `NetworkingComposition` freezes after the built-in registrations, so a
  failed built-in registration is fatal at startup.
- Lookups: `GetInfo<T>()`, `GetInfo(Type)`, and `GetInfo(Uuid)` return
  `Result<NetworkComponentInfo>`.
- `GetComponents(NetworkDirection)` returns the frozen array for one direction.
- Reads are lock-free after `Freeze`. Do not register concurrently with reads.

## `NetworkDirection`

`WaywardBeyond.Networking/Registry/NetworkDirection.cs`.

- `ServerOwned` — the server is authoritative; replicated downstream to clients.
- `ClientOwned` — the client is authoritative; replicated upstream to the
  server (e.g. input).

## Codecs

`WaywardBeyond.Networking/Codecs/`.

`IPayloadCodec.Serialize(DataStore, int)` returns `Result<byte[]>` and fails when
the entity does not carry the component. `IPayloadCodec.Apply(DataStore, int,
ReadOnlySpan<byte>)` returns `Result` and fails when the payload cannot be
applied. `IPayloadCodec<T>` adds the typed `Serialize(in T)` used by the client
drains.

- `NsdComponentCodec<T>` — the default; requires the component to be an nsd
  message. It binds the generated `Serialize()` and
  `Deserialize(ReadOnlySpan<byte>)` through delegates.
- Hand-written adapters — `TransformCodec`/`PhysicsCodec`/`IdentifierCodec`/
  `InventoryCodec`/`ParticleEmitterCodec`.

`PhysicsCodec` carries the body configuration (`Layer`, `BodyType`,
`CollisionDetection`) with velocity and angular velocity. `Apply` creates the
component when the entity has none, which is how a client materializes a body it
did not build itself. An existing component keeps its configuration and takes
only the motion values.

`TransformCodec` carries position, orientation, and scale. `Apply` rejects a
non-finite value and an orientation at or below the minimum squared length, so a
zero quaternion never reaches a body. `TransformCodec.IsValid` is the same check
the client uses for a remote player's locally simulated body.

`ParticleEmitterCodec` carries `EffectId`, `Playing`, `RateScale`, `Offset`,
`LocalDirection`, and `TintRgba` as `ParticleEmitterMessage`. `Apply` rejects an
empty, whitespace, or overlong `EffectId` (`> 128`), a negative or non-finite
`RateScale`, and non-finite vector fields, and leaves unchanged emitters clean.

Every registered `ServerOwned` component has one clear ownership rule. If a
networked entity carries it on the server, the publisher replicates its updates
and removals unconditionally without per-entity filter predicates. Clients derive
local colliders from level data for voxel structures and from
`BodyViewComponent` for remote characters, so `ColliderComponent` does not cross
the wire.

## `ReplicationAudience`

`WaywardBeyond.Networking/Registry/ReplicationAudience.cs` declares which
connected clients receive a `ServerOwned` component during publish fan-out for
deltas, full syncs, and component removals. The publisher serializes each
component once and delivers it according to the audience:

- `All` — every connected client receives updates and removals.
- `OwnerOnly` — only the client whose session entity matches the entity receives
  updates and removals.
- `OthersOnly` — every connected client except the entity's owning client
  receives updates and removals.
- `ViewersOnly` — only the entity's owning client (when it is a player session
  entity) and explicitly subscribed viewer clients receive updates, initial
  attach snapshots, removals, and targeted detach despawns. Non-viewers receive
  nothing, and dirty updates with no owner and no viewers skip serialization.

Only `ServerOwned` registrations accept `OwnerOnly`, `OthersOnly`, or
`ViewersOnly`.
`LaserBeamComponent` uses `OthersOnly`
(`WaywardBeyond.Networking/NetworkingComposition.cs:39-44`) so the server omits
the shooter's own beam updates and removals while the shooter renders a locally
predicted beam. `StatisticsComponent` uses `OwnerOnly`
(`WaywardBeyond.Networking/NetworkingComposition.cs:47-52`). The server sends a
player's vitals, attributes, and buff timers only to that owning session.
`InventoryComponent` (`WaywardBeyond.Networking/NetworkingComposition.cs:29-34`)
and `MachineStateComponent` (`WaywardBeyond.Networking/NetworkingComposition.cs:53-58`)
use `ViewersOnly`. Players receive their own `InventoryComponent`, and clients
subscribed to a server brick-entity receive that brick-entity's
`InventoryComponent` and `MachineStateComponent` while viewing it.

Every failure in `TransformCodec` and `PhysicsCodec` carries a message that
names the entity and the invalid field. The caller writes that message into its
log.

## Registered components

Wired in `WaywardBeyond.Networking/NetworkingComposition.cs`.

| Component | Uuid | Direction | Codec |
|---|---|---|---|
| `InputComponent` | 1 | ClientOwned | `NsdComponentCodec<InputComponent>` |
| `TransformComponent` | 2 | ServerOwned | `TransformCodec` |
| `PhysicsComponent` | 3 | ServerOwned | `PhysicsCodec` |
| `BodyViewComponent` | 10 | ServerOwned | `NsdComponentCodec<BodyViewComponent>` |
| `IdentifierComponent` | 11 | ServerOwned | `IdentifierCodec` |
| `EquipmentComponent` | 12 | ClientOwned | `NsdComponentCodec<EquipmentComponent>` |
| `InventoryComponent` | 13 | ServerOwned | `InventoryCodec` (`ViewersOnly`) |
| `GameModeComponent` | 14 | ServerOwned | `NsdComponentCodec<GameModeComponent>` |
| `InteractionEvent` | 15 | ClientOwned | `NsdComponentCodec<InteractionEvent>` |
| `InventoryEvent` | 16 | ClientOwned | `NsdComponentCodec<InventoryEvent>` |
| `LaserBeamComponent` | 17 | ServerOwned | `NsdComponentCodec<LaserBeamComponent>` (`OthersOnly`) |
| `ParticleEmitterComponent` | 18 | ServerOwned | `ParticleEmitterCodec` |
| `ParticleEmitterBindingComponent` | 19 | ServerOwned | `NsdComponentCodec<ParticleEmitterBindingComponent>` |
| `StatisticsComponent` | 20 | ServerOwned | `NsdComponentCodec<StatisticsComponent>` (`OwnerOnly`) |
| `MachineStateComponent` | 21 | ServerOwned | `NsdComponentCodec<MachineStateComponent>` (`ViewersOnly`) |

`InputComponent` (`WaywardBeyond.Data/Nsd/components.nsd:5-19`) serializes movement, look totals, sequence bookkeeping, `DampenersDisabled` (field `8`, default `false` for dampeners enabled), held action buttons, and `PaintId`.

Uuid allocation is stable and intentional. The sequence-gap at 4–9 and the
assigned ids starting at 10 are reserved. Do not reuse these ids.

## Remote player public view

`BodyViewComponent` (its `Body` is the body asset string ID) and the reused
engine `IdentifierComponent` (its `Name`) carry a joining client's minimal
public character view on the server player mirror. This is the appearance that
drives a remote player's billboard, plus the name. It never carries inventory
or attributes. The `IdentifierComponent` tag is `"game"`, the client teardown
key, so the mirror is freed with the rest of the gameplay world on exit.

The client renders any remote player (an entity with `BodyViewComponent` but no
`PlayerComponent`) through the general billboard path. `RemotePlayerVisualSystem`
resolves `Body` (a string ID) into the body's directional materials and attaches
a `BillboardComponent`, which `BillboardSystem` renders as a camera-facing
quad with the sector-facing material. See [asset-definitions](asset-definitions.md)
for the body data format.

The interaction context (`EquipmentComponent`, `InventoryComponent`,
`GameModeComponent`) is seeded from the client's local character save at join.
`EquipmentComponent` stays client-owned; the server validates interactions and
consumes items against its values. See [join](networking-join.md) and
[voxel-edits](networking-voxel-edits.md).

## Source of truth

- `WaywardBeyond.Networking/Registry/{NetworkRegistry,NetworkComponentInfo,NetworkDirection,ReplicationAudience}.cs`
- `WaywardBeyond.Networking/Codecs/{IPayloadCodec,IPayloadCodecOfT,NsdComponentCodec,InventoryCodec,TransformCodec,PhysicsCodec,IdentifierCodec,ParticleEmitterCodec}.cs`
- `WaywardBeyond.Networking/NetworkingComposition.cs`
- Component declarations: `WaywardBeyond.Data/Components/`
- Schemas: `WaywardBeyond.Data/Nsd/components.nsd`

## Tests that pin this

- `WaywardBeyond.Tests/StatisticsReplicationTests.cs` pins `OwnerOnly` audience
  filtering, full-sync and delta replication, and idle dirty suppression for
  `StatisticsComponent`.
- `WaywardBeyond.Tests/BrickEntityTests.cs` pins `ViewersOnly` registration for
  `InventoryComponent` and `MachineStateComponent`, initial attach snapshots,
  live deltas, targeted detach despawns, and non-viewer isolation.

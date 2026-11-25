# Networking — Registry

One subject: stable wire identity for networked components.

## `NetworkRegistry`

`WaywardBeyond.Networking/Registry/NetworkRegistry.cs` maps networked component
types to a stable wire uuid, an owning endpoint, and a payload codec. One
instance is registered as a DI singleton by `NetworkingComposition`; systems take
it through constructor injection.

- `Register<T>(Uuid, NetworkDirection, IPayloadCodec<T>)` is the only
  registration path. The typed codec ties the registration to the component at
  compile time. It returns `Result` with a contextual failure message (duplicate
  type, duplicate uuid, `Uuid.Null`).
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
  `ColliderCodec`/`ParticleEmitterCodec`.

`PhysicsCodec` carries the body configuration (`Layer`, `BodyType`,
`CollisionDetection`) with velocity and angular velocity. `Apply` creates the
component when the entity has none, which is how a client materializes a body it
did not build itself. An existing component keeps its configuration and takes
only the motion values.

`TransformCodec` carries position, orientation, and scale. `Apply` rejects a
non-finite value and an orientation at or below the minimum squared length, so a
zero quaternion never reaches a body. `TransformCodec.IsValid` is the same check
the client uses for a remote player's locally simulated body.

`ColliderCodec` carries a compound collider as packed shapes, offsets, and
orientations. A single shape travels as a one-part compound.

`ParticleEmitterCodec` carries `EffectId`, `Playing`, `RateScale`, `Offset`,
`LocalDirection`, and `TintRgba` as `ParticleEmitterMessage`. `Apply` rejects an
empty, whitespace, or overlong `EffectId` (`> 128`), a negative or non-finite
`RateScale`, and non-finite vector fields, and leaves unchanged emitters clean.

A registration may pass a replication filter. The publisher consults it before
it collects a delta or a full sync, so a component that only some entities carry
does not replicate for the others. `ColliderComponent` uses a filter: only an
entity with `BodyViewComponent` (a character body) replicates its collider. The
client derives a world structure's voxel collider from the level data, so those
never cross the wire.

Every failure in `TransformCodec`, `PhysicsCodec`, and `ColliderCodec` carries a
message that names the entity and the invalid field. The caller writes that
message into its log.

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
| `InventoryComponent` | 13 | ServerOwned | `NsdComponentCodec<InventoryComponent>` |
| `GameModeComponent` | 14 | ServerOwned | `NsdComponentCodec<GameModeComponent>` |
| `InteractionEvent` | 15 | ClientOwned | `NsdComponentCodec<InteractionEvent>` |
| `InventoryEvent` | 16 | ClientOwned | `NsdComponentCodec<InventoryEvent>` |
| `ColliderComponent` | 17 | ServerOwned | `ColliderCodec` (character bodies only) |
| `LaserBeamComponent` | 18 | ServerOwned | `NsdComponentCodec<LaserBeamComponent>` |
| `ParticleEmitterComponent` | 19 | ServerOwned | `ParticleEmitterCodec` |
| `ParticleEmitterBindingComponent` | 20 | ServerOwned | `NsdComponentCodec<ParticleEmitterBindingComponent>` |

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

- `WaywardBeyond.Networking/Registry/{NetworkRegistry,NetworkComponentInfo,NetworkDirection}.cs`
- `WaywardBeyond.Networking/Codecs/{IPayloadCodec,IPayloadCodecOfT,NsdComponentCodec,TransformCodec,PhysicsCodec,IdentifierCodec,ColliderCodec,ParticleEmitterCodec}.cs`
- `WaywardBeyond.Networking/NetworkingComposition.cs`
- Component declarations: `WaywardBeyond.Data/Components/`
- Schemas: `WaywardBeyond.Data/Nsd/components.nsd`

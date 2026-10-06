# Networking — Registry

One subject: stable wire identity for networked components.

## `NetworkRegistry`

`WaywardBeyond.Shared.Networking/Registry/NetworkRegistry.cs` maps networked
component types to a stable wire identity.

- `Initialize(assemblies)` scans assemblies for value-type `IDataComponent`
  structs annotated with `[NetworkComponent(uuid, direction)]` and registers
  each with an `NsdComponentCodec<T>` that drives the generated nsd serializers.
  Duplicates from the scan fail registration and the scan continues (mod-facing
  path: log and continue).
- `Register<T>(Uuid, NetworkDirection, IPayloadCodec)` is the explicit path for
  engine/third-party components that are not nsd messages. It returns a
  `Result` with a contextual failure message (duplicate type, duplicate uuid,
  `Uuid.Null`, invalid codec) instead of failing silently; the shared host
  wire-up treats a failed built-in registration as fatal at startup.
- Codec validity is proven at registration: the generated-nsd-methods check in
  `NsdComponentCodec<T>`'s static constructor is forced via
  `RunClassConstructor`, so a component without generated serialize/deserialize
  fails at registration, never mid-game on the wire.
- Reverse lookups: `TryGetInfo(Type)` / `TryGetInfo(Uuid)`.
- Enumeration by direction via `GetComponents(NetworkDirection)`. Replication
  systems cache the enumeration per instance so the per-tick hot path does not
  allocate (see [replication](networking-replication.md)).

Engine/third-party components are registered explicitly from the game wiring,
never from the engine.

## `NetworkDirection`

- `ServerOwned` — the server is authoritative; replicated downstream to clients.
- `ClientOwned` — the client is authoritative; replicated upstream to the
  server (e.g. input).

## Codecs

`IPayloadCodec` serializes a component into the opaque `byte[]` payload of a
`ComponentSnapshot` and applies a payload back onto an entity.

- `NsdComponentCodec<T>` — the default; requires the component to be an nsd
  message. Delegates to the generated `Serialize()`/`Deserialize`.
- Hand-written adapters — `TransformCodec`/`PhysicsCodec`/`IdentifierCodec`
  (in `Client.Core/Networking/`).

## Registered components

Wired in `Client.Core/Injector.cs` (`RegisterNetworking`).

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

Uuid allocation is stable and intentional. The sequence-gap at 4–9 and the
grouping of 10–16 are reserved; do not reuse.

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
`GameModeComponent`) is seeded from the client's local character save at join
and is server-owned thereafter. See [join](networking-join.md) and
[voxel-edits](networking-voxel-edits.md).

## Source of truth

- `WaywardBeyond.Shared.Networking/Registry/NetworkRegistry.cs`
- `WaywardBeyond.Shared.Networking/Registry/NsdComponentCodec.cs`
- `WaywardBeyond.Shared.Networking/Registry/{IPayloadCodec,NetworkComponentAttribute,NetworkComponentInfo,NetworkDirection}.cs`
- Wiring: `Client.Core/Injector.cs` (`RegisterNetworking`)

## Tests that pin this

- `Swordfish.Tests` codec/registry tests register + round-trip the networked
  components.
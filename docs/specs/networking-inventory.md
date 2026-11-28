# Networking — Inventory Operations

One subject: how inventory layouts and slot transfers replicate from client prediction to server authority.

## Model

The server owns inventory state (`InventoryComponent` is `ServerOwned`, UUID `13`). The client applies an intra-inventory move locally as immediate prediction. The server validates every op against its authoritative copy, applies it atomically, and replicates the updated sparse `InventoryComponent` so rejected moves reconcile automatically. Cross-container transfers execute only on the server. Client requests ride as discrete `InventoryEvent` snapshots.

Schema definitions live in `WaywardBeyond.Data/Nsd/components.nsd`. Shared runtime types live in `WaywardBeyond.Data/Components/` and `WaywardBeyond.Data/Inventory/`. Staging buffers live in `WaywardBeyond.Gameplay/Networking/`. Shared transfer resolution lives in `WaywardBeyond.Gameplay/Interactions/InventoryResolver.cs`.

## Keyed slot layout and sparse replication

`InventoryComponent` replaces flat arrays with keyed slot groups addressed by composite value type `SlotId(string GroupId, int LocalIndex)`:

```nsd
message SlotId
{
    string GroupId = 0;
    int LocalIndex = 1;
}

message SlotGroupDescriptor
{
    string GroupId = 0;
    int Capacity = 1;
}

message InventoryComponent
{
    SlotGroupDescriptor[] Groups = 0;
    WaywardBeyond.Data.KeyedSlotEntry[] Slots = 1;
}
```

- Canonical group IDs live in `WaywardBeyond.Data/Inventory/SlotGroups.cs:4`. Player inventories carry `wb:hotbar` (9), `wb:backpack` (36), `wb:wearables` (6), and `wb:augments` (5). Containers use machine or chest groups such as `wb:input`, `wb:output`, `wb:fuel`, and `wb:cargo`.
- Runtime lookups use a contiguous `ItemData[]` store indexed by `SlotGroupLayout` (`WaywardBeyond.Data/Components/SlotGroupLayout.cs:8`), capped at `SlotGroupLayout.MAX_TOTAL_CAPACITY` (`4096`) to reject forged wire layouts. Resizing a group rebuilds group offsets without shifting other groups' logical `SlotId` addresses (`WaywardBeyond.Data/Components/InventoryComponent.cs:259`).
- Only non-empty slots serialize into `InventoryComponent.Slots` (`WaywardBeyond.Data/Components/InventoryComponent.cs:436`). When `InventoryCodec.Apply` decodes an `InventoryComponent`, it calls `Hydrate()` to rebuild `_layout` and recover orphaned stacks (`WaywardBeyond.Networking/Codecs/InventoryCodec.cs:20-34`).
- Entries referencing removed groups, out-of-bounds indices, or colliding slots route to free `wb:backpack` slots or persist non-destructively in `Orphans` until capacity becomes available (`WaywardBeyond.Data/Components/InventoryComponent.cs:446-540`). `SyncWireState` drains eligible orphans into `wb:backpack` before building `Slots` (`WaywardBeyond.Data/Components/InventoryComponent.cs:606`), keeping wire round-trips idempotent between server and client.

## Data-driven schemas and tag filters

`SlotGroupSchemaDatabase` (`WaywardBeyond.Data/Inventory/SlotGroupSchemaDatabase.cs:14`) loads `assets/inventory/*.toml` files from mounted Shoal modules (`WaywardBeyond.Items/assets/inventory/slot_groups.toml`). Schemas specify group capacity, group-wide `AllowedTags`, and per-slot `SlotAllowedTags`. Filters match against invariant item tag lists (`ILocalizedTagsDatabase.Invariant`), such as `item.wearable.helmet` on `wb:wearables[0]`, `item.augment` on `wb:augments`, and `item.fuel` on `wb:fuel`.

## The envelope: `InventoryEvent`

```nsd
message InventoryEvent
{
    ulong Entity              = 0;
    uint  SequenceNumber      = 1;
    TransferOp? Transfer      = 2;
}
```

`InventoryEvent` is registered `ClientOwned` under UUID `16` (`WaywardBeyond.Networking/NetworkingComposition.cs:32`). Inbound ops stage onto `NetworkComponent.StagedInventoryOps` in `NetworkReplicationSystem` (`WaywardBeyond.Server/Systems/NetworkReplicationSystem.cs:704-710`). `ServerInventorySystem` drains staged ops in sequence order between `NetworkApplySystem` and interaction processing. Both the staged ring and `LastConsumedInventorySequence` live on `NetworkComponent` so reused player mirrors reset cleanly. Outbound, `ClientReplicationSystem` drains `PendingInventoryComponent.Outbound` (`WaywardBeyond.Client/Systems/ClientReplicationSystem.cs:183-216`) and clears ops only after snapshot acknowledgement.

## Unified transfer operation: `TransferOp`

```nsd
enum TransferMode
{
    Exact = 0;
    AutoStack = 1;
}

message TransferOp
{
    ulong FromContainer = 0;
    SlotId FromSlot = 1;
    ulong ToContainer = 2;
    SlotId ToSlot = 3;
    TransferMode Mode = 4;
    uint? Count = 5;
}
```

- **Exact** transfers up to `Count` items (or the whole stack when `Count` is null) from `FromSlot` on `FromContainer` to `ToSlot` on `ToContainer`. `Count` clamps to the source stack size before narrowing to `int`. Empty destinations receive a clamped stack, identical items merge up to stack capacity, and whole-stack moves onto different items swap only when both slots pass tag filters and stack limits.
- **AutoStack** (shift+click) drains the source stack across compatible same-item stacks first, then into the first empty permitted slot. Intra-inventory ops without an explicit target group target `wb:backpack` from `wb:hotbar`, `wb:hotbar` from `wb:backpack`, and `wb:backpack` then `wb:hotbar` from specialty groups. Setting `ToSlot.GroupId` targets a specific group across any valid slots in that group.

## Server validation and anti-dupe guarantees

`ServerInventorySystem` (`WaywardBeyond.Server/Systems/ServerInventorySystem.cs:84-254`) enforces these invariants before committing any change:

1. **Self-alias normalization**: container UUID `0` resolves to the acting player's entity UUID before selecting intra-container vs cross-container resolution, blocking self-aliasing duplication (`WaywardBeyond.Server/Systems/ServerInventorySystem.cs:97-105`).
2. **Player isolation**: transfers targeting any entity carrying `OwnedCharacterComponent` or an active session client are rejected (`WaywardBeyond.Server/Systems/ServerInventorySystem.cs:195-199`).
3. **Reach and access checks**: remote containers must carry finite `TransformComponent` positions within reach (`ContainerAccessComponent.MaxReach` when positive, else `CharacterDefaults.DEFAULT_REACH`). `ContainerAccessComponent` (`WaywardBeyond.Gameplay/Components/ContainerAccessComponent.cs:7`) also enforces `ActiveViewer`, `IsLocked` against non-owners, and `RequiredPermission` via `IUserPermissionService`.
4. **Filter and stack clamping**: `InventoryValidationContext` validates slot filter tags on destination inserts and bidirectional swaps, and clamps stack sizes against `IItemDatabase` authoritative `MaxStack` (`WaywardBeyond.Gameplay/Interactions/InventoryResolver.cs:274-354`). Undefined wire `TransferMode` values are rejected. Joins clamp seeded stacks identically (`WaywardBeyond.Server/Systems/ServerJoinSystem.cs:370`).
5. **Atomic copy-on-write**: resolvers mutate cloned copies (`InventoryComponent.Clone()`) only when staged ops exist and write back only when validation succeeds (`WaywardBeyond.Server/Systems/ServerInventorySystem.cs:66-193`).
6. **Dead-player rejection and Death-Container auto-despawn**: `ServerInventorySystem` discards staged inventory operations from players with `StatisticsComponent.IsDead` while advancing `LastConsumedInventorySequence`. When a cross-container transfer empties a remote source container carrying `MachineStatusFlags.AutoDespawnWhenEmpty`, `CommitRemoteSourceContainer` (`WaywardBeyond.Server/Systems/ServerInventorySystem.cs:195`) calls `ServerBrickEntityService.TryDespawnEmptyAutoDespawnContainer` and queues `LevelSaveService.QueueDeleteStructure`.

## The client producer and equipment indexing

The client applies moves through `PlayerData.ApplyMove` (`WaywardBeyond.Client/Player/PlayerData.cs:120`), which predicts intra-inventory transfers locally and stages them on `PendingInventoryComponent.Outbound`. Direct production mutation of `InventoryComponent` outside local construction and server authority is prohibited.

`EquipmentComponent` (UUID `12`, `ClientOwned`) remains strictly dedicated to `ActiveInventorySlot`, which indexes `wb:hotbar` (`WaywardBeyond.Client/Player/PlayerData.cs:100`). Brick picking (middle click) selects an existing `wb:hotbar` slot via `EquipmentComponent` or moves a `wb:backpack` stack to the active hotbar slot via `TransferOp`.

## Member reservation table

Reserved op members follow the nullable-union pattern on `InventoryEvent`:

| Member | Reserved shape | Behavior |
|---|---|---|
| `Transfer` (done) | `TransferOp?` | keyed Exact and AutoStack intra/cross-container transfers |
| `Sort` | `byte?` | sort a slot group (reserved) |
| `Drop` | `UuidInt3?` | drop items into the world (reserved) |

## Source of truth

- `WaywardBeyond.Data/Nsd/{components,saves}.nsd`
- `WaywardBeyond.Data/Inventory/{SlotGroups,SlotGroupSchemaDatabase,SlotGroupSchema}.cs`
- `WaywardBeyond.Items/assets/inventory/slot_groups.toml`
- `WaywardBeyond.Data/Components/{InventoryComponent,InventoryEvent,SlotGroupLayout,SlotId,TransferOp}.cs`
- `WaywardBeyond.Networking/Codecs/InventoryCodec.cs:9-34`
- `WaywardBeyond.Gameplay/Components/ContainerAccessComponent.cs:7`
- `WaywardBeyond.Gameplay/Networking/{NetworkComponent,InventoryOp,StageRingExtensions}.cs`
- `WaywardBeyond.Gameplay/Interactions/{InventoryResolver,InventoryValidationContext}.cs`
- `WaywardBeyond.Server/Systems/{ServerInventorySystem,ServerJoinSystem,PlayerInitialInventory,NetworkReplicationSystem}.cs`
- `WaywardBeyond.Client/Player/PlayerData.cs`
- `WaywardBeyond.Client/Systems/{ClientReplicationSystem,PlayerInteractionService}.cs`
- `WaywardBeyond.Client/UI/Layers/{Hotbar,Inventory}.cs`

## Tests that pin this

- `WaywardBeyond.Tests/KeyedInventoryTests.cs` pins group resizing without address shift, sparse wire round-trips, orphan recovery, TOML filter schemas, bidirectional swap rejection, stack clamping, self-alias anti-dupe, reach/viewer/lock/permission enforcement, and player isolation.
- `WaywardBeyond.Tests/InventoryResolverTests.cs` pins keyed Exact/AutoStack rules, distribution math, and empty slot lookup.
- `WaywardBeyond.Tests/NetworkComponentTests.cs` pins sequence watermarks and `ServerInventorySystem` execution.

# Persistence — SQLite Level Databases

One subject: how save data persists.

## Substrate

All save data lives in SQLite databases through `Microsoft.Data.Sqlite`.
`StoragePaths` (`WaywardBeyond.Data/StoragePaths.cs`) resolves the file
layout under `StorageConfig.SaveRoot` (`WaywardBeyond.Config/StorageConfig.cs`,
file `storage.toml`). The default value is the relative `saves/` path.
`StoragePaths` resolves a relative `SaveRoot` against `AppPaths.DataRoot`
(`Swordfish.Library/IO/AppPaths.cs`) and keeps a rooted `SaveRoot` unchanged.
The client launcher supplies the company name `bitfish`, so saves go under
`<LocalAppData>/bitfish/Wayward Beyond`. `SwordfishEngine.ResolveAppPaths`
(`Swordfish/SwordfishEngine.cs`) chooses the data root from the process
arguments. `--user-data <path>` uses the given directory, `--portable` uses the
working directory, and the default is the local user data folder. `--user-data`
wins when both flags are present. The engine logs the resolved roots at startup.
`AppEngine` requires an `AppPaths` value, so every host states where its data
lives. Pass a company name to `AppPaths.ForLocalUser` to nest the application
under a company folder. On Windows the root is `%LOCALAPPDATA%`, on Linux it is
`$XDG_DATA_HOME` or `~/.local/share`, and on macOS it is
`~/Library/Application Support`. The dedicated server uses
`AppPaths.ForWorkingDirectory()`, so its default saves stay in the working
directory and `--data` (`WaywardBeyond.Server.Launcher/Program.cs`) overrides
`SaveRoot`.

A `saves/` folder from an older build in the working directory is ignored. The
game is pre-release and this change is a clean break. Copy old saves into the
local user data root to keep them.

Connections are never pooled (`SqliteDatabase`, `WaywardBeyond.Data/Sqlite/SqliteDatabase.cs`),
so a disposed store releases its file handle and a level directory can be
removed on every platform. Each database runs with WAL, `synchronous=NORMAL`,
and a 5 second busy timeout.

## Layout

| Path | Owner | Tables |
|---|---|---|
| `saves/profile.db` | Client | `characters`, `level_metadata` |
| `saves/<levelGuid>/level.db` | Server | `level`, `entities`, `character_locations`, `brick_entities` |
| `blueprints/*.wbp` | Client | Binary nsd `BlueprintData` files (`FileBlueprintStorage`) |

See [region-blueprints](region-blueprints.md) for the client blueprint storage
contract and `StorageConfig.BlueprintRoot`.

The client owns `profile.db`. `characters` stores one raw nsd blob per
character uuid. `CharacterSaveManager.Save` (`WaywardBeyond.Client/Saves/CharacterSaveManager.cs:49-108`)
merges durable namespaced replicated stats from `StatisticsComponent` into
`Character.Statistics` (`WaywardBeyond.Data/Nsd/saves.nsd:61`) through
`StatisticsPersistence.MergeClientStatistics`. S.P.A.C.E.R. attributes
(`wb:strength`, `wb:precision`, `wb:awareness`, `wb:charisma`, `wb:education`,
`wb:resolve`) live directly in `Character.Statistics` from character creation.
The merge preserves skill XP and counters and strips transient `wb:pressurized`,
`wb:buff.<id>.expires_tick`, `wb:buff.<id>.remaining_ticks`, and
`wb:cooldown.<itemId>` keys.
`level_metadata` stores
one raw nsd blob per level guid; the
model is `LevelMetadata` (`WaywardBeyond.Data/Nsd/saves.nsd`), a wrapper
around the shared `Playtime` value so client-local per-level fields can be
added later without touching the authoritative records. The client registers
the stores behind the existing interfaces (`ICharacterStorage`,
`ILevelMetadataStorage`) in `WaywardBeyond.Client/Injector.cs`.

The server owns one database per level. `SqliteLevelCatalog`
(`WaywardBeyond.Server/Saves/SqliteLevelCatalog.cs`) creates, lists,
deletes, and opens them. `SqliteLevelStorage`
(`WaywardBeyond.Data/Levels/SqliteLevelStorage.cs`) implements `ILevelStorage`
over one level database.

## Level generation

`SqliteLevelCatalog.Create` (`WaywardBeyond.Server/Saves/SqliteLevelCatalog.cs:46`)
invokes `LevelGenerator.Generate` (`WaywardBeyond.Server/Generation/LevelGenerator.cs:59`)
with the level seed, `GameMode`, and `LevelSpawn` (`GameplayConfig.DefaultSpawnPosition`).

- Every new level generates deterministic asteroid structures encoded with a portable brick palette.
- New `GameMode.Adventure` levels also spawn a starter wreckage structure from `LevelGeneratorConfig.StarterWreck.BlueprintId` (default `builtin/wrecked_lifeboat` in `WaywardBeyond.Server/assets/blueprints/builtin/wrecked_lifeboat.wbp`), loaded headlessly via `IBundledBlueprintDatabase`.
- `LevelGenerator` decodes the blueprint against `IBrickRegistry` and optional `IPaintRegistry`, centers it with `VoxelRegionApplier.ToCenteredVoxelObject`, places and orients it deterministically in front of `LevelSpawn` outside the player collider clearance radius, assigns a seed-derived non-zero `Uuid` disjoint from asteroid indices (`0x8000_0001u` tag), and encodes it with portable brick and paint palettes via `VoxelCodec.Encode`.
- Asteroid placement rejects candidates that intersect either the `LevelSpawn` clearance sphere or the starter wreck clearance sphere.
- `GameMode.Creative` levels do not spawn the starter wreck.
- Missing or unreadable starter blueprint data logs a structured warning and skips the wreck instead of failing level creation.

## `level.db` schema

| Table | Key | Payload |
|---|---|---|
| `level` | one row, guid column | serialized `Level` metadata |
| `entities` | entity uuid (text) | serialized `VoxelEntityData` |
| `character_locations` | character uuid (text) | serialized `CharacterEntityData` |
| `brick_entities` | composite `(voxel_entity_uuid, voxel_x, voxel_y, voxel_z)` | serialized `BrickEntityData` (`WaywardBeyond.Data/Nsd/saves.nsd:108`) |

Keys are decimal strings because SQLite integers are signed 64-bit and the
uuids are `ulong`. A player mirror is allocated with the character's uuid, so
the character id and the mirror's entity uuid are one value. `character_locations`
carries that value in `CharacterEntityData.Uuid`, so the row key and the payload
identity agree (`ILevelStorage.Save` keys on it; `GetCharacterEntity` reads by it).

## Save semantics

A full level save commits one transaction (`ILevelStorage.Save`):
  - The `level` row upserts.
  - Every `entities` row is replaced by the captured snapshot. A structure
  removed from the world no longer persists.
  - Every `brick_entities` row is replaced by the captured `BrickEntityData`
  snapshot (`ServerBrickEntityService.Capture`,
  `WaywardBeyond.Server/Systems/ServerBrickEntityService.cs:564`). Each record
  stores the anchor `(VoxelEntityUuid, VoxelX, VoxelY, VoxelZ)`, `BrickId`,
  sparse keyed `InventoryComponent` entries, and persistent `MachineStateComponent`
  fields (`StationTag`, `ActiveRecipeId`, `Progress`, `Duration`, `PowerEnabled`,
  and `MachineStatusFlags.PersistentMask` such as door `Open`). Transient runtime
  state (`HasPower` and non-persistent status flags) resets on load.
  - Each captured `character_locations` row upserts. Rows absent from the
  capture are never deleted. A character not present in the world at save time
  keeps its last location.

`LevelSaveService.LoadLevel` (`WaywardBeyond.Server/Saves/LevelSaveService.cs:119-203`)
reads all `brick_entities` rows and delegates each loaded structure to
`ServerBrickEntityService.RestoreStructure`
(`WaywardBeyond.Server/Systems/ServerBrickEntityService.cs:355`). Every placed
brick claimed by an `IServerBrickEntityDecorator` allocates a server ECS
brick-entity with `BrickAnchorComponent` and decorator-provisioned components
(`InventoryComponent`, `ContainerAccessComponent`, `MachineStateComponent`),
restoring matching saved data when present. When every structure decodes cleanly,
any saved `brick_entities` row whose structure no longer exists or whose brick at
that coordinate no longer matches is deleted through
`ILevelStorage.DeleteBrickEntities`
(`WaywardBeyond.Data/Levels/SqliteLevelStorage.cs:194`).

The store hands out deserialized models, not bytes. `ILevelStorage.GetLevel`
returns the level, `GetVoxelEntities` returns the level's registered voxel
entities, and `GetCharacterEntity` returns a character's location. Each read is
version-guarded and migrated forward by the injected `Migrator`, so callers do
not deserialize or migrate.

`LevelSaveService` (`WaywardBeyond.Server/Saves/LevelSaveService.cs`) captures the
authoritative world on the server thread and submits the blocking writes to a
worker. It tracks the in-flight save. `Flush` awaits it and then writes, so a
pending capture can never land after a newer snapshot. `Dispose` awaits the
pending save and closes the store handle. A capture carries the storage it was
taken from, so a queued write always lands in its own level database. The world
unload path is: `Unload` drains the pending save, disposes the store, a level
switch opens the next level through the catalog, and server shutdown flushes
each world before its container is disposed.

`MarkActive` and `EndSessionStamp` are small writes that only touch the `level`
row. `QueueSaveCharacterLocation` (`WaywardBeyond.Server/Saves/LevelSaveService.cs:200`)
calls `CaptureCharacter` (`WaywardBeyond.Server/Saves/LevelSaveService.cs:407-429`) to
capture transform, scale, game mode, and surviving `StatisticsComponent` entries on the
server thread and queues a single `character_locations` upsert on the save worker.
`StatisticsPersistence.CaptureServerStatistics` excludes `wb:pressurized`,
`wb:cooldown.<itemId>`, and expired `wb:buff.<id>.expires_tick` entries
(`expires_tick <= currentSimTick`), and converts active
buff expiry ticks into tick-independent `wb:buff.<id>.remaining_ticks` counts so server
restarts preserve remaining buff duration. It keeps the
pending record in memory until the worker commits it, so an immediate rejoin reads the
latest transform, game mode, and `CharacterEntityData.Statistics`
(`WaywardBeyond.Data/Nsd/saves.nsd:105`) through `GetCharacterEntity`. A character with
no row in the level starts at `Level.Spawn` with `Level.DefaultGameMode`.
`CharacterStatisticsSeeder.SeedAuthoritativeStatistics` (`WaywardBeyond.Gameplay/Statistics/CharacterStatisticsSeeder.cs:192`)
seeds vitals and restores `wb:buff.<id>.remaining_ticks` relative to the world's current
sim tick from `CharacterEntityData.Statistics` on rejoin, or seeds from bounded
`CharacterState.Statistics` (`WaywardBeyond.Data/Nsd/levels.nsd:77`) and defaults on first
join. `ServerJoinSystem` queues a save whenever a player leaves,
disconnects, or replaces an active session. Character locations and surviving stats are
also captured and upserted with each full save. The playtime stamping rules are unchanged
(`PlayClock.Advance`).

### Server autosave

`ServerSaveSystem` (`WaywardBeyond.Server/Systems/ServerSaveSystem.cs`) queues a level
flush every `GameplayConfig.AutosaveIntervalMs` while a world has sessions. It
broadcasts `notification.level.saving` when the capture is queued, then
`notification.level.saved` or `notification.level.saveFailed` when the
background write finishes. A world with no sessions does not autosave; the
unload path flushes it.

## Level delete

A menu-time delete is an RPC call. `ServerHostCommands` unloads the world on the
server thread through `ServerWorldHost.UnloadLevel`. A loaded level is torn
down without a final save, because a queued save would recreate the files after
deletion. Bound peers lose their world binding. `ServerLevelManager` then
deletes the level directory on the catalog worker. The call returns
`RpcResult<DeleteLevelResponse>`.

## Client facade

`GameSaveService` (`WaywardBeyond.Client/Saves/`) is a thin client facade: a
cached save listing from `ListLevelsRequest`, with `CreateSaveAsync`/`Delete`/
`TriggerServerSave` routed to the server via `LevelsClient`
(`WaywardBeyond.Client/Networking/LevelsClient.cs`). `CreateSaveAsync` forwards
the chosen `DefaultGameMode` and returns
the created `GameSave` after the listing refreshes, so `NewSavePage` can set the
active save and open the crew manifest. `SelectSavePage` pulls the listing when
it opens and re-pulls it every second while it stays visible, so a level another
client creates appears without leaving the page. The listing response also
carries `CanCreateSave`, which `GameSaveService.CanCreateSave` exposes so
`SelectSavePage` hides Chart new galaxy when the server denies the
`waywardbeyond.level.create` key. The client tracks its own per-save "last
played" and "time played" in the `level_metadata` table, merged over the server's
level metadata in `GameSaveService.GetSaves()`. Character save is handled by
`CharacterSaveManager` + `SqliteCharacterStorage`. The client also records the
last played archive's card facts in the `[LastSession.Save]` table of
`profile.toml`, so the main menu can show the Continue card before the listing
loads. Deleting the recorded save or colonist
clears those markers.

`LevelsClient` is a thin async wrapper over `GameServerApiClient`. Calls are
correlated by nsd-rpc, so there is no FIFO queue or waiter table. A dropped
connection faults pending calls. Every call rides the reliable send queue, so a
create, list, delete, or save is never silently dropped.

Game save logic lives in `GameSaveManager` (`WaywardBeyond.Client/Saves/GameSaveManager.cs`), which owns the client save
cadence. Its autosave timer saves the character only. Pause, F5, and close call
`Save()`, which saves the character and, on the local host only, sends a
`SaveLevelAsync`. Remote clients never request a level save. The server
autosaves the level.

Character inventories persist sparsely as `KeyedSlotEntry[]? Inventory` on
`Character` (`WaywardBeyond.Data/Nsd/saves.nsd:63`) and `CharacterState`
(`WaywardBeyond.Data/Nsd/levels.nsd:74`). `CharacterSaveManager` writes
`ToSparseEntries()` on save (`WaywardBeyond.Client/Saves/CharacterSaveManager.cs:80`).
A `null` `Inventory` signals a new character and grants the starter loadout on
join (`WaywardBeyond.Server/Systems/ServerJoinSystem.cs:353`). An empty non-null
array signals an intentionally emptied inventory that never restocks.

## Serialization and data versioning

`Migrator` (`WaywardBeyond.Data/Migrations/Migrator.cs`) gates on
`DataVersion.CURRENT` and runs per-record forward migrations. Storage types
guard and migrate on read: `SqliteCharacterStorage`, `SqliteLevelMetadataStorage`,
and `SqliteLevelStorage` all refuse records stamped by a newer build. Structure
data carries a brick palette since data version 4. `VoxelCodec` encodes live
registry-id data to a palette on write and decodes on load. The `Character` and
`Level` records stamp their format via the `Version` message.
See [brick-identity](brick-identity.md).

The previous NATS JetStream store is no longer read. Existing JetStream data in
the old install `saves/` folder is ignored; the game is pre-release and this
change is a clean break. No importer ships.

## Multi-process behavior

Two game processes that share a data root can both open `profile.db` and the
per-level databases. SQLite locking makes concurrent access safe. Delete is
coordinated on the owning server thread within one process; two processes
managing the same root must not delete each other's loaded levels. There is
no shared broker process to manage.

## Source of truth

- `Swordfish.Library/IO/AppPaths.cs`
- `WaywardBeyond.Config/StorageConfig.cs`
- `WaywardBeyond.Data/StoragePaths.cs`
- `WaywardBeyond.Data/Sqlite/SqliteDatabase.cs`
- `WaywardBeyond.Data/Levels/SqliteLevelStorage.cs`
- `WaywardBeyond.Data/Characters/SqliteCharacterStorage.cs`
- `WaywardBeyond.Data/Levels/SqliteLevelMetadataStorage.cs`
- `WaywardBeyond.Data/Blueprints/FileBlueprintStorage.cs`
- `WaywardBeyond.Server/Saves/SqliteLevelCatalog.cs`
- `WaywardBeyond.Server/Generation/LevelGenerator.cs`
- `WaywardBeyond.Server/Saves/LevelSaveService.cs`
- `WaywardBeyond.Server/Systems/{IServerBrickEntityDecorator,InventoryBrickEntityDecorator,MachineBrickEntityDecorator,ServerBrickEntityService,ServerBrickEntitySystem}.cs`
- `WaywardBeyond.Gameplay/BrickEntityId.cs`
- `WaywardBeyond.Gameplay/Components/BrickAnchorComponent.cs`
- `WaywardBeyond.Data/Components/{MachineStateComponent,MachineStatusFlags}.cs`
- `WaywardBeyond.Data/Levels/BrickEntityKey.cs`
- `WaywardBeyond.Server/ServerLevelManager.cs`
- `WaywardBeyond.Server/ServerHostCommands.cs`
- `WaywardBeyond.Server/ServerWorldHost.cs`
- `WaywardBeyond.Data/Nsd/{saves,voxels,levels,blueprints}.nsd`
- `WaywardBeyond.Data/Statistics/{StatisticIds,StatisticsPersistence}.cs`
- `WaywardBeyond.Data/Components/{StatisticsComponent,StatisticsComponentExtensions}.cs`
- `WaywardBeyond.Gameplay/Statistics/CharacterStatisticsSeeder.cs`
- `WaywardBeyond.Client/Saves/CharacterSaveManager.cs`
- `WaywardBeyond.Server/Systems/ServerJoinSystem.cs`

## Tests that pin this

- `WaywardBeyond.Tests/AdventureStarterWreckTests.cs` pins Adventure starter wreckage persistence, Creative omission, determinism, palette round-trips, and spawn/asteroid clearance.
- `WaywardBeyond.Tests/KeyedInventoryTests.cs` pins sparse keyed character storage persistence round-trips.
- `WaywardBeyond.Tests/CharacterLocationSaveTests.cs` pins join inventory seeding and non-restocking rules.
- `WaywardBeyond.Tests/ServerJoinStatisticsTests.cs` pins join stat seeding, Adventure Mode bounds, expired buff pruning on leave and rejoin, and client durable stat merging.
- `WaywardBeyond.Tests/BrickEntityTests.cs` pins `level.db` `brick_entities` save/load round-trips keyed by `(VoxelEntityUuid, Int3)`, starter wreck and blueprint brick-entity allocation with cargo seeding, and orphaned record pruning.
- No tests pin the playtime rules.
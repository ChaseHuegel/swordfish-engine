# Persistence — SQLite Level Databases

One subject: how save data persists.

## Substrate

All save data lives in SQLite databases through `Microsoft.Data.Sqlite`.
`StoragePaths` (`WaywardBeyond.Data/StoragePaths.cs`) resolves the file
layout under a data root. The root defaults to the relative `saves/` directory
and comes from `StorageConfig` (`WaywardBeyond.Config/StorageConfig.cs`,
file `storage.toml`). The dedicated server can override it with `--data`
(`WaywardBeyond.Server.Launcher/Program.cs`).

Connections are never pooled (`SqliteDatabase`, `WaywardBeyond.Data/Sqlite/SqliteDatabase.cs`),
so a disposed store releases its file handle and a level directory can be
removed on every platform. Each database runs with WAL, `synchronous=NORMAL`,
and a 5 second busy timeout.

## Layout

| Path | Owner | Tables |
|---|---|---|
| `saves/profile.db` | Client | `characters`, `level_metadata` |
| `saves/<levelGuid>/level.db` | Server | `level`, `entities`, `character_locations` |

The client owns `profile.db`. `characters` stores one raw nsd blob per
character uuid. `level_metadata` stores one raw nsd blob per level guid; the
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

## `level.db` schema

| Table | Key | Payload |
|---|---|---|
| `level` | one row, guid column | serialized `Level` metadata |
| `entities` | entity uuid (text) | serialized `VoxelEntityData` |
| `character_locations` | character uuid (text) | serialized `CharacterEntityData` |

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
  - Each captured `character_locations` row upserts. Rows absent from the
  capture are never deleted. A character not present in the world at save time
  keeps its last location.

The store hands out deserialized models, not bytes. `ILevelStorage.GetLevel`
returns the level, `GetVoxelEntities` returns the level's registered voxel
entities, and `GetCharacterEntity` returns a character's location. Each read is
version-guarded and migrated forward by the injected `Migrator`, so callers do
not deserialize or migrate.

`LevelSaveService` (`WaywardBeyond.Server/Saves/LevelSaveService.cs`) captures the
authoritative world on the server thread and submits the blocking writes to a
worker. It tracks the in-flight save. `Flush` awaits it and then writes, so a
pending capture can never land after a newer snapshot. `Dispose` awaits the
pending save and closes the store handle. The world unload path is:
`Unload` disposes the store, a level switch opens the next level through the
catalog, and server shutdown flushes each world before its container is
disposed.

`MarkActive` and `EndSessionStamp` are small writes that only touch the `level`
row. Character locations are captured with the full save and upserted with it.
The playtime stamping rules are unchanged (`PlayClock.Advance`).

### Server autosave

`ServerSaveSystem` (`WaywardBeyond.Server/Systems/ServerSaveSystem.cs`) queues a level
flush every `GameplayConfig.AutosaveIntervalMs` while a world has sessions. It
broadcasts `notification.save.saving` when the capture is queued, then
`notification.save.saved` or `notification.save.saving.failed` when the
background write finishes. A world with no sessions does not autosave; the
unload path flushes it.

## Level delete

A menu-time delete is an RPC call. `ServerHostCommands` handles it on the
server thread and calls `ServerWorldHost.DeleteLevel`. A loaded level is torn
down without a final save, because a queued save would recreate the files after
deletion. Bound peers lose their world binding. The catalog then deletes the
level directory. The call returns `RpcResult<DeleteLevelResponse>`.

## Client facade

`GameSaveService` (`WaywardBeyond.Client/Saves/`) is a thin client facade: a
cached save listing from `ListLevelsRequest`, with `CreateSave`/`Delete`/
`TriggerServerSave` routed to the server via `LevelsClient`
(`WaywardBeyond.Client/Networking/LevelsClient.cs`). `SelectSavePage` pulls the
listing when it opens and re-pulls it every second while it stays visible, so a
level another client creates appears without leaving the page. The listing
response also carries `CanCreateSave`, which `GameSaveService.CanCreateSave`
exposes so `HomePage` hides "Create save" when the server denies the
`waywardbeyond.level.create` key. The client tracks its own per-save "last
played" and "time played" in the `level_metadata` table, merged over the server's
level metadata in `GameSaveService.GetSaves()`. Character save is handled by
`CharacterSaveManager` + `SqliteCharacterStorage`.

`LevelsClient` is a thin async wrapper over `GameServerApiClient`. Calls are
correlated by nsd-rpc, so there is no FIFO queue or waiter table. A dropped
connection faults pending calls. Every call rides the reliable send queue, so a
create, list, delete, or save is never silently dropped.

`GameSaveManager` (`WaywardBeyond.Client/Saves/GameSaveManager.cs`) owns the client save
cadence. Its autosave timer saves the character only. Pause, F5, and close call
`Save()`, which saves the character and, on the local host only, sends a
`SaveLevelAsync`. Remote clients never request a level save; the server
autosaves the level.

## Serialization and data versioning

`Migrator` (`WaywardBeyond.Data/Migrations/Migrator.cs`) gates on
`DataVersion.CURRENT` and runs per-record forward migrations. Storage types
guard and migrate on read: `SqliteCharacterStorage`, `SqliteLevelMetadataStorage`,
and `SqliteLevelStorage` all refuse records stamped by a newer build. Structure
data carries a brick palette since data version 4; `VoxelEntityDataCodec`
encodes live registry-id data to a palette on write and decodes on load. The
`Character` and `Level` records stamp their format via the `Version` message.
See [brick-identity](brick-identity.md).

The previous NATS JetStream store is no longer read. Existing `saves/`
JetStream data is ignored; the game is pre-release and this change is a clean
break. No importer ships.

## Multi-process behavior

Two game processes that share a data root can both open `profile.db` and the
per-level databases. SQLite locking makes concurrent access safe. Delete is
coordinated on the owning server thread within one process; two processes
managing the same root must not delete each other's loaded levels. There is
no shared broker process to manage.

## Source of truth

- `WaywardBeyond.Config/StorageConfig.cs`
- `WaywardBeyond.Data/StoragePaths.cs`
- `WaywardBeyond.Data/Sqlite/SqliteDatabase.cs`
- `WaywardBeyond.Data/Levels/SqliteLevelStorage.cs`
- `WaywardBeyond.Data/Characters/SqliteCharacterStorage.cs`
- `WaywardBeyond.Data/Levels/SqliteLevelMetadataStorage.cs`
- `WaywardBeyond.Server/Saves/SqliteLevelCatalog.cs`
- `WaywardBeyond.Server/Saves/LevelSaveService.cs`
- `WaywardBeyond.Server/ServerLevelManager.cs`
- `WaywardBeyond.Server/ServerHostCommands.cs`
- `WaywardBeyond.Server/ServerWorldHost.cs`
- `WaywardBeyond.Data/Nsd/{saves,voxels,levels}.nsd`

## Tests that pin this

- No tests pin the playtime rules. The game test suite is unmaintained.
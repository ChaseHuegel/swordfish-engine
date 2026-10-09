# Persistence — SQLite Level Databases

One subject: how save data persists.

## Substrate

All save data lives in SQLite databases through `Microsoft.Data.Sqlite`.
`StoragePaths` (`WaywardBeyond.Data/StoragePaths.cs`) resolves the file
layout under a data root. The root defaults to the relative `saves/` directory
and comes from `StorageSettings` (`WaywardBeyond.Config/StorageSettings.cs`,
file `storage.toml`). The dedicated server can override it with `--data`
(`WaywardBeyond.Server.Launcher/Program.cs`).

Connections are never pooled (`SqliteDatabase`, `WaywardBeyond.Data/SqliteDatabase.cs`),
so a disposed store releases its file handle and a level directory can be
removed on every platform. Each database runs with WAL, `synchronous=NORMAL`,
and a 5 second busy timeout.

## Layout

| Path | Owner | Tables |
|---|---|---|
| `saves/profile.db` | Client | `characters`, `save_meta` |
| `saves/<levelGuid>/level.db` | Server | `level`, `entities`, `character_locations` |

The client owns `profile.db`. `characters` stores one raw nsd blobs per
character id. `save_meta` stores one raw nsd blob per level guid. The client
registers the stores behind the existing interfaces (`ICharacterStorage`,
`ISaveMetaStorage`) in `WaywardBeyond.Client/Injector.cs`.

The server owns one database per level. `SqliteLevelCatalog`
(`WaywardBeyond.Server/Saves/SqliteLevelCatalog.cs`) creates, lists,
deletes, and opens them. `SqliteLevelStore`
(`WaywardBeyond.Data/SqliteLevelStore.cs`) implements `ILevelStore`
over one level database.

## `level.db` schema

| Table | Key | Payload |
|---|---|---|
| `level` | one row, guid column | serialized `Level` metadata |
| `entities` | entity uuid (text) | serialized `VoxelEntityData` |
| `character_locations` | character id (text) | serialized `CharacterEntityData` |

Keys are decimal strings because SQLite integers are signed 64-bit and the
ids are `ulong`.

## Save semantics

A full level save commits one transaction (`ILevelStore.WriteSave`):

- The `level` row upserts.
- Every `entities` row is replaced by the captured snapshot. A structure
  removed from the world no longer persists.
- Each captured `character_locations` row upserts. Rows absent from the
  capture are never deleted. A character not present in the world at save time
  keeps its last location.

`LevelSaveService` (`Server.Core/Saves/LevelSaveService.cs`) captures the
authoritative world on the server thread and submits the blocking writes to a
worker. It tracks the in-flight save. `Flush` awaits it and then writes, so a
pending capture can never land after a newer snapshot. `Dispose` awaits the
pending save and closes the store handle. The world unload path is:
`Unload` disposes the store, a level switch opens the next level through the
catalog, and server shutdown flushes each world before its container is
disposed.

`SaveLocation`, `MarkActive`, and `EndSessionStamp` are small writes that only
touch the `level` or `character_locations` rows. The playtime stamping rules
are unchanged (`SaveTime.Accumulate`).

### Server autosave

`ServerWorldSystem` (`Server.Core/Systems/ServerWorldSystem.cs`) queues a level
flush every `GameplaySettings.AutosaveIntervalMs` while a world has sessions. It
broadcasts `notification.save.saving` when the capture is queued, then
`notification.save.saved` or `notification.save.saving.failed` when the
background write finishes. A world with no sessions does not autosave; the
unload path flushes it.

## Level delete

Menu-time delete requests flow through `ServerLevelManager`
(`Server.Core/ServerLevelManager.cs`) into `PendingLevelDeletes`
(`Server.Core/PendingLevelDeletes.cs`). `ServerWorldHost` drains the queue on
the server thread. A loaded level is torn down without a final save, because a
queued save would recreate the files after deletion. Bound connections return
to `PendingJoins`. The catalog then deletes the level directory and the host
answers `DeleteLevelResponse`.

## Client facade

`GameSaveService` (`Client.Core/Saves/`) is a thin client facade: a cached
save listing from `ListLevelsRequest`, with `CreateSave`/`Delete`/
`TriggerServerSave` routed to the server via `LevelsClient`
(`Client.Core/Networking/LevelsClient.cs`). The client tracks its own per-save
"last played" and "time played" in the `save_meta` table, merged over the
server's level metadata in `GameSaveService.GetSaves()`. Character save is
handled by `CharacterSaveManager` + `SqliteCharacterStorage`.

`GameSaveManager` (`Client.Core/Saves/GameSaveManager.cs`) owns the client save
cadence. Its autosave timer saves the character only. Pause, F5, and close call
`Save()`, which saves the character and, on the local host only, sends a
`SaveLevelRequest`. Remote clients never request a level save; the server
autosaves the level.

## Serialization and data versioning

`SaveMigrator` (`WaywardBeyond.Data/Saves/SaveMigrator.cs`) gates on
`SaveVersion.CurrentDataVersion` and runs per-record forward migrations.
`SqliteCharacterStorage` and `SqliteLevelCatalog` refuse records stamped by a
newer build. Structure data carries a brick palette since data version 4;
`VoxelEntityDataCodec` encodes live FNV voxel ids to a palette on write and
decodes on load. See [brick-identity](brick-identity.md).

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

- `WaywardBeyond.Config/StorageSettings.cs`
- `WaywardBeyond.Data/StoragePaths.cs`
- `WaywardBeyond.Data/SqliteDatabase.cs`
- `WaywardBeyond.Data/SqliteLevelStore.cs`
- `WaywardBeyond.Data/SqliteCharacterStorage.cs`
- `WaywardBeyond.Data/SqliteSaveMetaStorage.cs`
- `WaywardBeyond.Server/Saves/SqliteLevelCatalog.cs`
- `WaywardBeyond.Server/Saves/LevelSaveService.cs`
- `WaywardBeyond.Server/ServerLevelManager.cs`
- `WaywardBeyond.Server/PendingLevelDeletes.cs`
- `WaywardBeyond.Data/CodeGen/{saves,voxels,levels}.nsd`

## Tests that pin this

- Store round trips, snapshot entity replacement, and location preservation in
  `Swordfish.Tests/SqliteStorageTests.cs`.
- Server autosave cadence, completion reporting, and save authorization in
  `Swordfish.Tests/Permissions/ServerWorldSaveTests.cs`.
- Server-owned level save/load and the join stream in
  `Swordfish.Tests/ServerJoinStreamTests.cs`.
- Save-meta accumulation rules in
  `WaywardBeyond.Client.Tests/SaveTimeTests.cs`.
- Character playtime frames in
  `WaywardBeyond.Client.Tests/CharacterSaveManagerTests.cs`.
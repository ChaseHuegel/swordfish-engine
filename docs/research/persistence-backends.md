# Research — Persistence Backends (NATS vs sqlite)

One subject: whether NATS-backed KV stays the backing for game saves and
character saves, and what replaces it. This is a standalone review document
for user review. The decision is recorded here (issue #0036).

## Current layout (before the decision)

- **Store.** Every save flowed through a NATS-backed `KeyValueStore`
  (`WaywardBeyond.Data/KeyValueStore.cs`) with an embedded
  `PersistentNatsProcess` (bundled `nats-server`, `saves/` storage dir). Two
  buckets: `levels` (level data, per level guid) and `characters`
  (per character uuid), plus save metadata.
- **Game saves.** Owned by `WorldSaveService` (`Server.Core/Saves`): the
  authoritative world flushed on save requests, leave, disconnect, and server
  shutdown. Save frequency: on-demand (menu save), autosave interval, and
  teardown - not continuous.
- **Character saves.** Owned by `CharacterSaveManager` + `NatsCharacterStorage`
  (`Client.Core/Saves`): small records written on save/leave/disconnect.

## Costs and constraints (the original review)

- **Embedded NATS:** one child process per game process, a NATS connection
  from the client store and the server store, JetStream disk usage in
  `saves/`, firewall prompts on Windows, orphan-process risk, and shared
  process takeover between clients on one machine.
- **sqlite:** `Microsoft.Data.Sqlite` is already referenced by
  `WaywardBeyond.Data`, giving transactions and atomic single-file
  storage with no process. The planned live-streaming guarantees were never
  on the wire path: NATS KV is per-key atomic, not cross-key, and the save
  path uses no JetStream consumers.

## Decision (implemented)

NATS is removed entirely. The game persists to SQLite:

- `saves/profile.db` holds the client-owned `characters` and `save_meta`
  tables.
- One `saves/<levelGuid>/level.db` per level holds `level`, `entities`, and
  `character_locations`.
- `LevelSaveService`/`SqliteLevelCatalog`/`SqliteLevelStore` replace
  `WorldSaveService`/`KeyValueStore`.
- The bundled `nats-server` and `PersistentNatsProcess` are deleted.
- The old JetStream store is not migrated: the game is pre-release, so this
  is a clean break.

The data root is `StorageSettings.SaveRoot`, default `saves/`, overridable by
config (`storage.toml`) and by the dedicated server's `--data` flag.

The implementation details, layout, and semantics live in
[specs/persistence](../specs/persistence.md).

No document other than this one is modified by this research (issue #0036).
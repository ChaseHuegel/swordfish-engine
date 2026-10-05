# Research — Persistence Backends (NATS vs sqlite)

One subject: whether NATS-backed KV stays the backing for game saves and
character saves, or whether character saves move to sqlite. This is a
standalone review document for user review; no other doc or issue is changed
by this research's output.

## Current layout

- **Store.** Every save flows through a NATS-backed `KeyValueStore`
  (`WaywardBeyond.Shared.Data/KeyValueStore.cs`) with an embedded
  `PersistentNatsProcess` (bundled `nats-server`, `saves/` storage dir). Two
  buckets: `levels` (world data, per level guid) and `characters`
  (per character id), plus save metadata.
- **Game saves.** Owned by `WorldSaveService` (`Server.Core/Saves`): the
  authoritative world flushes on save requests, leave, disconnect, and
  server shutdown. World data is chunked (`VoxelEntityDataComponent.Chunks`)
  and packet-sized; a full-world flush is the dominant write. Save frequency:
  on-demand (menu save), autosave interval, and teardown - not continuous.
- **Character saves.** Owned by `CharacterSaveManager` + `NatsCharacterStorage`
  (`Client.Core/Saves`): small records (inventory, statistics, active slot,
  time played) written on save/leave/disconnect - small, infrequent,
  per-character.

## Costs and constraints

- **Embedded NATS:** one child process per game process (`#0022` lifecycle),
  a NATS connection from the client store and the server store, JetStream
  disk usage in `saves/`. The child is load-bearing for the dedicated
  launcher (`#0035`) and per-world save lifetimes (`#0008`).
- **sqlite:** `Swordfish.Integrations` already ships `Microsoft.Data.Sqlite`
  (`Swordfish.Integrations`), giving transactions and atomic single-file
  storage with no process.

## Recommendation

| Store | Backend | Why |
|---|---|---|
| Game saves (`levels`) | **NATS KV stays** | Efficient streaming of large chunked world data through the existing KV surface; the dedicated server and per-world lifecycles already build on it. No measured advantage to refiling. |
| Character saves (`characters`) | **sqlite** | Small records, transactionality (a crash mid-write cannot corrupt the character), simpler than a process-backed KV for per-character files, reuse of the shipped sqlite package. |
| Save metadata | sqlite alongside characters | Same transaction as the character write where applicable. |

## Migration path

1. `ICharacterStorage` gains a sqlite implementation
   (`CharacterSaveManager` already targets the interface - swap the
   registration; no client-world changes).
2. Keep `NatsCharacterStorage` code until the next save-format version, then
   remove; data shapes are versioned already (`CharacterSeed`/`ItemData`
   are wire-stable types).
3. Ship the migration as a one-time reader: new reader that imports a
   character from the old `characters` bucket when absent.
4. `#0022`, `#0035`, `#0008` need no change: NATS stays for worlds; the
   dedicated launcher still embeds NATS for `levels`.

## Open questions for the user

- Is a one-time import from the old bucket required, or is a clean
  character reset acceptable for a pre-release game?
- Should sqlite tables live per character file or one database with a
  characters table?

No document other than this one is modified by this research (issue #0036).
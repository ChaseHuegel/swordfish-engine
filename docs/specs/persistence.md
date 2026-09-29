# Persistence — NATS KV Buckets

One subject: how save data persists.

## Substrate

All save data flows through a local NATS JetStream server launched by
`PersistentNatsProcess` (`Server.Core/Streaming/PersistentNatsProcess.cs`,
started from the client `Entry`), wrapped by `KeyValueStore` (NATS KV,
sync-over-async) in `WaywardBeyond.Shared.Data/KeyValueStore.cs`.

Environment config (in `KeyValueStore.cs:16-18`): `NATS_URL`, `NATS_JWT`,
`NATS_NKEY_SEED`; default `nats://127.0.0.1:4222`. Buckets are auto-created on
first use.

`KeyValueStore` operations: `Put<T>`, `Get<T>`, `GetKeys`, `Delete` (single and
bulk). It is sync-over-async (blocks on `.Task.Result`), so a full-world save
must be throttled/submitted to a worker and not run inline on the server tick.

## Buckets

| Bucket | Owner | Key pattern | Payload |
|---|---|---|---|
| `characters` | Client | `<characterId>` | `Character` |
| `saves` | Client | `<levelGuid>` | `SaveMeta` |
| `levels` | Server | `<levelGuid>` | `Level` meta |
| `levels` | Server | `<guid>.entity.<uuid>` | `VoxelEntityData` |
| `levels` | Server | `<guid>.character.<characterId>` | `CharacterEntityData` (spawn location) |

## `saves` bucket

`NatsSaveMetaStorage` (`WaywardBeyond.Shared.Data/NatsSaveMetaStorage.cs`,
`BUCKET_NAME = "saves"` at line 9). Key = `<levelGuid>`, value = serialized
`SaveMeta` (`LastPlayedMs`, `AgeMs`).

Client-owned. Each process runs its own local NATS, so this bucket is per-client
by construction: two clients joining the same multiplayer save each track their
own "last played" and "time played" for it. `ISaveMetaStorage` is the interface
contract, mirrored on the `characters` bucket.

## `characters` bucket

`NatsCharacterStorage` (`WaywardBeyond.Shared.Data/NatsCharacterStorage.cs`,
`BUCKET_NAME = "characters"` at line 8). Key = `id.ToString()`, value =
serialized `Character` (via `Character.Serialize()`).

Owned by the client. It is the source of the join-time seed — see
[join](networking-join.md). `ICharacterStorage` is the interface contract.

The character record carries its own playtime clock (`LastPlayedMs`, `AgeMs`),
accumulated with the same `SaveTime.Accumulate` rule. The clock re-stamps to the
current wall-clock at session start. `CharacterSaveManager.Load` does the stamp,
called from `GameSaveManager.Load` when a character joins a world. The stamp
means the character's time played measures session time only. It never counts
the idle gap since the previous session.

Skill XP persists only here, as `Character.Statistics` entries (skill id → total
XP). The client writes the server-authoritative totals from each
`SkillStateUpdateMessage` (see [skills](skills.md)); the server stores no skill
data of its own.

## `levels` bucket

`WorldSaveService` (`Server.Core/Saves/WorldSaveService.cs`,
`BUCKET_NAME = "levels"` at line 26). Server-owned.

Key layout (confirmed at the cited source-of-truth lines):

- `<levelGuid>` → serialized `Level` meta (Version, Seed, spawn, GameMode, Name).
  Written at `WorldSaveService.cs:77`.
- `<guid>.entity.<uuid>` → serialized `VoxelEntityData` (chunked voxels +
  transform), one per structure. Written at `WorldSaveService.cs:81`.
- `<guid>.character.<characterId>` → serialized `CharacterEntityData`
  (authoritative location). Written at `WorldSaveService.cs:198`.

Operations: `CreateWorld` (runs the shared `WorldGenerator`, persists Level meta
+ one entity per structure; the seed string is normalized to an int via
+ `WorldGenerator.HashSeed`, so typed, randomized, and non-numeric seeds all
+ create a deterministic world), `ListLevels`, `DeleteLevel`, `LoadLevel` (builds
authority bodies via `VoxelWorldEntityFactory`), `SaveLocation` (sampled from
the server-authoritative transform), `QueueWorldSave`/`Flush` (autosave + flush
on server stop).

The server owns the save's aggregate playtime metadata. `LastPlayedMs` stamps
to the current wall-clock when anyone joins (`MarkActive`, called from
`ServerJoinSystem`). `AgeMs` accumulates through every world save (the level
meta rides the `QueueWorldSave`/`Flush` capture) and whenever a player leaves
or disconnects (`EndSessionStamp`, called from `ServerJoinSystem` and
`ServerContext`). The aggregate `AgeMs` therefore represents a total across all
players' sessions. The in-memory stamp is synchronous; the metadata KV write is
submitted to a worker, so the server tick never blocks on the store. Share the
stamping rule via `SaveTime.Accumulate`; a zero last-played stamps no time, so
the epoch never leaks into the age.

## Server shutdown cascade

The sequencing point is explicit: client window close → client requests server
stop → server flushes world save → server thread exits → NATS process stops →
process exits. The flush must be awaited before `PersistentNatsProcess` dispose
(Shoal dispose order is unspecified), to avoid a save-vs-teardown race.

## Client facade

`GameSaveService` (`Client.Core/Saves/`) is a thin client facade: a cached save
listing from `ListWorldsRequest`, with `CreateSave`/`Delete`/`TriggerServerSave`
routed to the server via `WorldsClient`. The client tracks its own per-save
"last played" and "time played" in the `saves` bucket, merged over the server's
level metadata in `GameSaveService.GetSaves()` (no client meta uses a
never-stamped save) and updated on join and on every save/leave by
`GameSaveManager`. Character save is handled by `CharacterSaveManager` +
`NatsCharacterStorage`. The old world-gen/load/save stages are gone.

## Serialization

Shared DTOs live in `WaywardBeyond.Shared.Data/CodeGen/{saves,voxels,world}.nsd`
(`Character`, `Level`, `VoxelEntityData`/`Chunk`/`Voxel`,
`CharacterEntityData`). No sqlite, no loose files (except a legacy disk-migration
path retained in `GameSaveService`).

### Brick palette and data versioning

A world structure (`VoxelEntityData`) carries a brick palette since data version
4: `BrickPalette[n]` is the brick name for voxel id `n`
(`voxels.nsd`). `WorldSaveService` encodes live FNV voxel ids into a palette on
write and decodes a palette back to the local id space on load, via
`VoxelEntityDataCodec` (`Shared.Gameplay/Saves/`). The palette makes saved voxel
ids self-describing and stable across content changes. See
[brick-identity](brick-identity.md).

`SaveMigrator` (`WaywardBeyond.Shared.Data/Saves/SaveMigrator.cs`) gates on
`SaveVersion.CurrentDataVersion` (`Shared.Data/SaveVersion.cs`, value `4`) and
runs per-record forward migrations. `WorldSaveService.LoadLevel` refuses a level
stamped by a newer build, migrates and decodes each loaded structure, and
`ListLevels` skips newer-format levels. `NatsCharacterStorage` gates and migrates
characters. The v3→v4 structure migration is
`VoxelEntityDataV3ToV4Migration` (`Shared.Gameplay/Saves/`).

The optional SQL layer (`Swordfish.Integrations/SQL/`) is not used by the save
path.

## Source of truth

- `WaywardBeyond.Shared.Data/KeyValueStore.cs`
- `WaywardBeyond.Shared.Data/NatsCharacterStorage.cs`
- `WaywardBeyond.Shared.Data/NatsSaveMetaStorage.cs`
- `WaywardBeyond.Shared.Data/SaveTime.cs`
- `WaywardBeyond.Shared.Data/SaveVersion.cs`
- `WaywardBeyond.Shared.Data/Saves/SaveMigrator.cs`
- `WaywardBeyond.Shared.Gameplay/Saves/VoxelEntityDataCodec.cs`
- `WaywardBeyond.Server.Core/Saves/WorldSaveService.cs`
- `WaywardBeyond.Server.Core/Streaming/PersistentNatsProcess.cs`
- `WaywardBeyond.Shared.Data/CodeGen/{saves,voxels,world}.nsd`

## Tests that pin this

- Save-meta accumulation rules in `WaywardBeyond.Client.Core.Tests/SaveTimeTests.cs`.
- Character playtime frames in `WaywardBeyond.Client.Core.Tests/CharacterSaveManagerTests.cs`.
- Character save/load round-trips in `WaywardBeyond.Client.Core.Tests`.
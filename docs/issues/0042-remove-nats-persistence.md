# Improvement: Remove NATS persistence; use SQLite per-level databases

- Type: improvement
- Status: done
- Workflow: ../specs/issues.md

## Problem

Every save flowed through a NATS JetStream server wrapped by
`KeyValueStore`. The embedded process cost outweighed its value: a bundled
`nats-server` child per game process (binaries under `assets/server/nats/`),
a supervisor with share/takeover logic (#0022, #0040), Windows firewall
prompts, no macOS/ARM support, and an opaque, non-portable store. The save
path never used JetStream consumers, and NATS KV is per-key atomic rather
than cross-key, so the all-or-nothing guarantee it was chosen for was not
delivered.

## Decision

Remove NATS entirely. Persist to SQLite:

- Client-owned `saves/profile.db` with `characters` and `save_meta` tables.
- One server-owned `saves/<levelGuid>/level.db` per level with `level`,
  `entities`, and `character_locations` tables.
- Save as one transaction. Entities are snapshot-replaced. Character
  locations are upsert-only, so a character absent from the world at save
  time keeps its last location.
- Deleting a loaded level tears it down on the server thread without a final
  save (a queued save would recreate the files), then removes the directory.
- Data root is `StorageSettings.DataRoot` (default `saves/`), overridable via
  `storage.toml` and the dedicated server's `--data`.
- The old JetStream store is ignored. No importer ships (pre-release clean
  break).
- "Level" names the save-data domain; "World" stays reserved for ECS
  terminology.

## Acceptance criteria

- [x] No NATS code, binaries, package, or env keys remain in the game.
- [x] Characters, save metadata, and level saves round-trip through SQLite.
- [x] A level save replaces entity rows and preserves absent character
      locations.
- [x] Deleting a loaded level unloads it and removes its files.
- [x] `--data` and `storage.toml` override the data root.
- [x] `dotnet build` and the game test suites pass.
- [x] The `World`/`Level` terminology split is applied to save-data types and
      docs.
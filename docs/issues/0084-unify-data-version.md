# Improvement: Unify the game data version

- Type: improvement
- Status: open
- Workflow: ../specs/issues.md

## Problem

`SqliteLevelCatalog` builds a `Data.Version` from `DataVersion.CURRENT`, a
literal name, and a literal environment (`WaywardBeyond.Server/Saves/SqliteLevelCatalog.cs`).
`WaywardBeyond.Version` holds the same information. The two can drift, and the
server stamps every new level with whichever copy it read.

A level written by a build whose version block disagrees with the storage
version gate can be refused on read, or migrated when it should not be.

## Acceptance criteria

- [ ] One type owns the game version, and the server reads it from there.
- [ ] The environment is not a hardcoded literal in the server catalog.
- [ ] A test pins that a created level and the `DataVersion.CURRENT` gate agree.

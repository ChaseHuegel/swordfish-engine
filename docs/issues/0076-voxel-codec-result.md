# Improvement: VoxelCodec reports failure through Result

- Type: improvement
- Status: open
- Workflow: ../specs/issues.md

## Problem

`VoxelCodec.Encode` and `VoxelCodec.Decode`
(`WaywardBeyond.Voxels/VoxelCodec.cs`) return `VoxelEntityData` in every case.

`Encode` returns `source` unchanged when the data already carries a palette. A
second encode is a silent no-op. A caller cannot tell a fresh encode from a
skipped one.

The four call sites cannot distinguish success from a pass-through either:

- `WaywardBeyond.Server/Saves/LevelSaveService.cs` on load and on save
- `WaywardBeyond.Server/Generation/AsteroidGenerator.cs`
- `WaywardBeyond.Server/ServerHostCommands.cs`
- `WaywardBeyond.Client/Systems/ClientJoinSystem.cs`

A structural problem with the input, such as a chunk with no voxels array,
currently either throws or is silently carried through.

Expected failure returns `Result` or `Result<T>` per `docs/development.md`.

## Acceptance criteria

- [ ] `Encode` and `Decode` return `Result<VoxelEntityData>`.
- [ ] A pass-through encode is a documented success, or it is a distinct
      outcome the caller can see.
- [ ] Each call site checks `Success` and logs a warning on failure.
- [ ] A failed decode does not enter the store or the level stream.
- [ ] Tests cover a palette-less input, a palette input, and a chunk with an
      absent voxels array.

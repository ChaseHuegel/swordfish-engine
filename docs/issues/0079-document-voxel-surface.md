# Improvement: document the voxel public surface

- Type: improvement
- Status: open
- Workflow: ../specs/issues.md

## Problem

`WaywardBeyond.Voxels` is a shared assembly that both the client and the server
compile against. Its public surface has almost no contract documentation.

Missing a summary: `VoxelObject`, `ChunkData`, `VoxelSample`, `VoxelPalette`,
`ShapeLight`, `ChunkVoxel`, `Int3`, `Short3`, `VoxelExtensions`, the
`VoxelObject` constructors, `Set`, `Get`, `Sample`, `GetEnumerator`,
`GetChunkInfos`, `ChunkData.GetSampler`, `ChunkData.ToChunkInfo`,
`VoxelPalette.Increment`, `Decrement`, `Any`, `Only`, `Count`, `Shape`,
`LightLevel`, `HasAny`, `GetShapeLight`, and `GetLightLevel`.

Missing `<inheritdoc/>`: the `IEquatable<Orientation>` implementation, the
`Equals`/`GetHashCode`/`ToString` overrides on `Orientation`, and `Dispose` and
`MoveNext` on both enumerators.

`docs/development.md` requires a one-line summary on every public or internal
non-obvious type and member, and `<inheritdoc/>` on every interface
implementation and override.

The type-level conventions that do not belong in a member comment, such as the
chunk coordinate system and the `Ahead = +Z` direction, belong in the voxel
specification instead. This issue covers the code-side contract only.

## Acceptance criteria

- [ ] Every public type and member in `WaywardBeyond.Voxels` has a one-line
      summary that states the contract, not the implementation.
- [ ] Every override and interface implementation carries `<inheritdoc/>`.
- [ ] No summary restates its member name or explains a decision.
- [ ] No code comment references `docs/`, an issue, or a plan.

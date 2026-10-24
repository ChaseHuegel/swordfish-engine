# Improvement: Remove per-interaction allocations in the server interaction path

- Type: improvement
- Status: open
- Workflow: ../specs/issues.md

## Problem

Two allocation sources remain on the interaction path, which runs per staged
interaction edge rather than per tick.

`ServerInteractionSystem` builds a new `IVoxelInteractionWorld` for every
interaction through `Func<DataStore, IVoxelInteractionWorld>`. The world wrapper
is stateless over the store, so one instance per store is enough.

`PlayerInteractionService` resolves the local player through a capturing lambda
in a store query per interaction, and builds a fresh world per call. The lambda
allocates a closure.

Neither is a per-tick cost, so this is not a regression. It is wasted work on a
path that a fast-breaking player can drive many times per second.

## Acceptance criteria

- [ ] The voxel interaction world is built once per store, not per interaction.
- [ ] The client resolve path uses a struct action instead of a capturing lambda.
- [ ] No allocation per interaction remains, except the resolution itself.

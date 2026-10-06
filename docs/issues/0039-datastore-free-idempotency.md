# Bug: DataStore.Free can double-free an entity index

- Type: bug
- Status: open
- Workflow: ../specs/issues.md

## Problem

`DataStore.Free` unconditionally clears the uuid and enqueues the index in
`_recycledEntities` / `_recycledSet` (`Swordfish.ECS/DataStore.cs:91`). A second
`Free` on the same index enqueues it again. A later `AllocNewEntity`
(`DataStore.cs:294`) dequeues it and hands the index to a new owner while the
first owner may still hold it, so two entities share one index and uuid.

The server can double-free. After `WorldSaveService.Unload` frees a player
mirror without clearing the session (`WorldSaveService.cs:572`),
`ServerJoinSystem.HandleJoin` frees the stale session entity again
(`ServerJoinSystem.cs:154`). Session mappings that outlive their entity make
this reachable.

This is an engine defect. Fix it in its own engine-first commit, before the
game-side session hardening that relies on it.

## Acceptance criteria

- [ ] `Free` on an entity whose uuid is already `Uuid.Null` is a no-op and never
      re-enqueues the index.
- [ ] A regression test frees an index twice and asserts the index is never
      allocated to two live entities.

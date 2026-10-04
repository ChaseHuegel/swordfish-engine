# Improvement: Component-level removal is not replicated (no wire shape, no public store API)

- Type: improvement
- Status: open
- Workflow: ../specs/issues.md

## Problem

`WorldSnapshot` carries only `RemovedEntities`; a component removed from a
live entity server-side cannot be expressed on the wire, and the store has
no public single-component removal surface (`DataStore` exposes
`Free(entity)` only). In scope by decision: component removal must
replicate, and a bare entity (no components) must survive removal - only
`RemovedEntities` despawns entities.

Locked design:

- **Store:** `DataStore.Remove<T>(int entity)` / `Remove(Type, int)`
  public surface over the existing `ChunkedStore.SetAt(..., exists:false)`
  path (value-preserving, `EXISTS` cleared, `DIRTY` set - already
  implemented, `ChunkedStore.cs:58-62`).
- **Publish rule:** per networked entity per registered ServerOwned type -
  `dirty && exists` -> `ComponentSnapshot`; `dirty && !exists` ->
  `ComponentRemoval`; clear dirty in both; never both in one tick. Freed
  entities excluded (uuid cleared by `Free`; despawns stay on
  `RemovedEntities`).
- **Wire:** new `ComponentRemoval { ulong Entity, ulong TypeUuid }`;
  `ComponentRemoval[] RemovedComponents = 4` on `WorldSnapshot`. Ordering
  against component deltas is inherent (same snapshot).
- **Apply:** client resolves (uuid -> entity, typeUuid ->
  `NetworkRegistry.TryGetInfo` -> `info.Type`) and calls
  `store.Remove(type, entity)`; unknown uuids skip; bare entities persist.

## Acceptance criteria

- [ ] Store removal surfaces implemented and wired so removals flow
      through the same dirty polling as updates.
- [ ] Publish and apply per the locked design; a removal in tick T can
      never be resurrected by a pre-removal delta (ordering pinned by
      same-snapshot delivery).
- [ ] Tests: remove-on-live-entity replicates and clears client-side; the
      entity survives removal as a bare entity; despawn
      (`Free`/`RemovedEntities`) is unchanged.
- [ ] Engine-first commit rule: the `Swordfish.ECS` change is committed
      separately, first, standalone-green (`Swordfish.Tests` passing),
      before any `WaywardBeyond` commit that uses it.
- [ ] `networking-messages.md` and `networking-replication.md` updated
      (docs pass).
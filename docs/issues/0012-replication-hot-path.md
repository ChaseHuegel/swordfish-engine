# Improvement: Replication hot-path allocations and scans scale with world size, not dirtiness

- Type: improvement
- Status: done
- Workflow: ../specs/issues.md

## Problem

The replication path does work proportional to world size every tick
regardless of how much actually changed:

- `NetworkRegistry.GetComponents(direction)` (`NetworkRegistry.cs:108-123`)
  allocates a fresh `List<NetworkComponentInfo>` per call, and both
  replication systems call it **per entity per tick** -
  `NetworkReplicationSystem.OnTickAction` (`:227`),
  `CollectFullStateAction` (`:267`), `ClientReplicationSystem` (`:65`). A
  100-entity world with 10 server-owned components allocates 1000 lists
  per tick even when nothing is dirty.
- `ClientReplicationSystem.Tick` scans the **entire client world** with an
  unfiltered `store.Query(0f, ref action)` (`:33`) - `DataStore.Query`
  iterates every allocated slot `1.._lastEntity` (`DataStore.cs:117-127`).
  Only the local player carries client-owned components; every structure
  pays the scan.
- `IsDirty`/`ClearDirty`/`MarkDirty` (`DataStore.cs:194-233`) each take
  the global `_chunkAndStoreLock` per call - per component, per entity,
  per tick, re-entering a lock the surrounding query already holds.
- The docs (`networking-replication.md`) describe `QueryDirty<T>` as the
  iteration model, but no system uses it. `DataStore` also exposes a
  per-store `Get(int)` that boxes every component into `IDataComponent`
  (`:173-192`) - used by the full-state path.

## Acceptance criteria

- [x] Client replication work is proportional to dirty client-owned
      components (player-scoped query or `QueryDirty`), not world entity
      count; a no-input world with 5000 entities costs the same per frame
      as an empty one.
- [x] Server publish hoists the `ServerOwned` component enumeration to
      once per tick (cached array), and per-entity dirty checks do not
      acquire the global store lock per check (batched or bulk dirty
      query).
- [x] Full-state collection avoids per-entity boxing (`store.Get(int)`),
      or is measured and bounded for join-time only.
- [x] Benchmark or test pins per-frame work: replication cost is flat when
      nothing is dirty, growing only with dirty components.
- [x] `networking-replication.md` describes the iteration model the code
      actually uses.
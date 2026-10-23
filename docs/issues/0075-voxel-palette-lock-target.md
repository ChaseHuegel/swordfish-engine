# Improvement: the voxel palette locks on a dictionary

- Type: improvement
- Status: open
- Workflow: ../specs/issues.md

## Problem

`VoxelPalette` (`WaywardBeyond.Voxels/VoxelPalette.cs`) is a `readonly struct`
that wraps a `Dictionary<ushort, int>` and guards every operation with
`lock (_voxelCounts)`.

`docs/development.md` requires `System.Threading.Lock` for monitor-based
synchronisation on this target framework. The guideline also forbids using an
object whose identity is meaningful as a lock target.

The palette is only reachable through a `VoxelObject`, and a `VoxelObject` is
owned by one thread. So the lock protects nothing today. The palette should
either follow the container's ownership rule and drop the lock, or use
`System.Threading.Lock` if a second thread is ever allowed to touch it.

This is the remainder of the container synchronisation review. The
`ReaderWriterLockSlim`, the upgradeable-read/write pair, and the
`Monitor.Enter` on the voxel array all go with the sampling contract change, and
this issue becomes small once that lands.

## Acceptance criteria

- [ ] `VoxelPalette` uses `System.Threading.Lock`, or it takes no lock and its
      doc comment states the ownership rule.
- [ ] No `lock` statement targets a field whose identity carries meaning.
- [ ] A test exercises the palette from two threads when the lock stays.

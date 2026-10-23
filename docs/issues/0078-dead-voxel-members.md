# Improvement: delete dead voxel members and settle container lifetime

- Type: improvement
- Status: open
- Workflow: ../specs/issues.md

## Problem

`WaywardBeyond.Voxels` is a new shared assembly with no callers for several
public members.

- `Orientation.Pitch`, `Orientation.Yaw`, and `Orientation.Roll` have no caller.
- `Orientation.ToEulerAngles` has no caller, and it returns degrees while the
  rest of the type works in radians. The name does not state the unit.
- `VoxelObject.Dispose` has no caller. `VoxelComponent` and `VoxelWorldComponent`
  hold a `VoxelObject` and `AddOrUpdate` replaces it without disposal, so the
  `IDisposable` contract is never honoured. The object owns a
  `ReaderWriterLockSlim` today, which holds wait handles under contention.

A public member with no caller is cost without a return. A lifetime contract
that no caller honours is worse, because it looks like a guarantee.

## Acceptance criteria

- [ ] The unused `Orientation` members are deleted, or gain a caller and a unit
      in the name.
- [ ] `VoxelObject` either drops `IDisposable` or has one owner that disposes it.
      The ECS components state which.
- [ ] No public member of `WaywardBeyond.Voxels` lacks a caller outside its own
      file, except a type member required by an interface.

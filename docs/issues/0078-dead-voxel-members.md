# Improvement: delete dead voxel members and state container ownership

- Type: improvement
- Status: open
- Workflow: ../specs/issues.md

## Problem

`WaywardBeyond.Voxels` is a new shared assembly with no callers for several
public members.

- `Orientation.Pitch`, `Orientation.Yaw`, and `Orientation.Roll` have no caller.
- `Orientation.ToEulerAngles` has no caller, and it returns degrees while the
  rest of the type works in radians. The name does not state the unit.

A public member with no caller is cost without a return.

The container has a second, smaller problem. `VoxelComponent` and
`VoxelWorldComponent` both hold a `VoxelObject`, and neither type states who owns
it or when it stops being reachable. `VoxelObject` no longer implements
`IDisposable`, so nothing releases the resident chunks when a component is
replaced or an entity is freed. The container is owned by the world that holds
the component, and that should be written down where both the client and the
server can see it.

## Acceptance criteria

- [ ] The unused `Orientation` members are deleted, or gain a caller and a unit
      in the name.
- [ ] `VoxelComponent` and `VoxelWorldComponent` state which world owns the
      container and when it becomes garbage.
- [ ] Replacing a component with a second `VoxelObject` does not leave the first
      reachable, or the doc comment explains why it is safe.
- [ ] No public member of `WaywardBeyond.Voxels` lacks a caller outside its own
      file, except a type member required by an interface.

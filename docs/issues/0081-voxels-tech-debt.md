# Improvement: harden the voxel module

- Type: improvement
- Status: open
- Workflow: ../specs/issues.md

## Problem

Three small defects in `WaywardBeyond.Voxels` are not worth a change each, and
none belongs to the sampling contract work.

`ShapeLight(BrickShape shape, int lightLevel)`
(`WaywardBeyond.Voxels/ShapeLight.cs`) packs a shape and a light level into one
byte without masking either nibble. It is correct only because `BrickShape` has
eight members and `Brick.Brightness` is `15` or `0`. One new shape past index
15, or one configurable brightness above 15, spills into the other nibble and
corrupts both. Masking both inputs is two lines and removes the dependency.

`WaywardBeyond.Voxels` needs four generated types from `WaywardBeyond.Data` but
inherits that project's whole dependency set: `Microsoft.Data.Sqlite`,
`CsvHelper`, `Swordfish.ECS`, and `Swordfish.Library`. A consumer that wants a
voxel container also takes a database driver. Note the split option for if a
second consumer appears. Do not act on it now.

`Orientation.ToEulerAngles` returns degrees with no unit in the name. This
disappears if the dead-member issue deletes the method. Keep it here only if the
method survives.

## Acceptance criteria

- [ ] `ShapeLight` masks the shape to four bits and the light level to four
      bits.
- [ ] A test constructs a `ShapeLight` with an out-of-range shape and light
      level and asserts the nibbles stay separate.
- [ ] The dependency weight of `WaywardBeyond.Voxels` is recorded in the module
      map, with the split condition stated.
- [ ] `Orientation.ToEulerAngles` states its unit in the name, or it is deleted.

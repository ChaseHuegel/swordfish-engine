# Improvement: preserve unknown bricks across a save round trip

- Type: improvement
- Status: open
- Workflow: ../specs/issues.md

## Problem

A saved structure records brick names in `BrickPalette`. Two resolution paths
turn an unknown name into the empty voxel.

- `VoxelCodec.BuildPalette` writes an empty string when the registry has no name
  for a voxel id.
- `VoxelCodec.PaletteIdToLocalId` returns 0 when the palette entry is empty or
  absent.

So a modded save loaded by a vanilla client loses every modded brick, and a
re-save persists the loss. The same happens for a brick that was removed from
the content set.

The content survives in the palette as a name. The voxels that used it do not
survive the trip.

A user who loads a modded save in a vanilla game should get their structure back
when they return to the modded client. They should also see that something is
missing while they are in the vanilla game.

## Acceptance criteria

- [ ] Decoding an unknown brick name allocates a data id for that name. The
      allocation is stable for the lifetime of the process and does not disturb
      the existing id space.
- [ ] Encoding a structure that holds an unresolved id writes the original name
      back, so the round trip is lossless.
- [ ] A brick whose name is unresolved renders with a missing-texture
      placeholder instead of disappearing.
- [ ] `IBrickRegistry` states whether the allocated ids are part of its public id
      space or a separate extension space.
- [ ] Tests load palette data whose names the registry does not know, re-encode
      it, and assert the names and voxel count survive.

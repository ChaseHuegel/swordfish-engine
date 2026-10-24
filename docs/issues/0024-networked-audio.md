# Improvement: Break/place sounds don't play for remote or authoritative edits

- Type: improvement
- Status: done
- Workflow: ../specs/issues.md

## Problem

Playtest: block break and place sounds aren't networked. `SoundEffectService`
break/place sounds (`SoundEffectService.cs:62-77`) are played only on the
client prediction path (`PlayerInteractionService`); the authoritative
apply path - `ClientVoxelReconcileSystem.WriteVoxel`
(`ClientVoxelReconcileSystem.cs:167-179`) - applies every
`VoxelEditMessage` silently, so a remote player's mining and construction
is inaudible to everyone else. The local player's own edits already sound
via prediction, so the fix must not double-play on the confirmation echo.

## Acceptance criteria

- [x] Authoritative `VoxelEditMessage` application plays the matching
      break/place sound (effects channel), determined by the resulting
      voxel (empty = break, filled = place; material-class variants
      preserved if the sound service exposes them).
- [x] No double-play: edits correlating to the local player's own pending
      predictions (confirm or snap in `ClientVoxelReconcileSystem`) play
      no sound - the prediction already did; only unpredicted (remote)
      edits play.
- [x] Tests: applying a remote-authored edit plays the expected sound;
      confirming an own pending echo plays none; a reverted prediction
      plays none.
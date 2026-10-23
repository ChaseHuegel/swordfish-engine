# Improvement: Split CharacterDefaults by concern

- Type: improvement
- Status: open
- Workflow: ../specs/issues.md

## Problem

`WaywardBeyond.Gameplay/CharacterDefaults.cs` owns unrelated concerns in one
type: inventory layout (`HOTBAR_SLOT_COUNT`), interaction reach, movement tuning,
character height and eye/foot offsets, a physics-component factory, and a
collider-component factory. The client and the server consume it for different
reasons.

Two heights disagree. The collider is built from `PLAYER_FLYING_HEIGHT` (0.75)
unconditionally, while the remote-player billboard uses
`PLAYER_STANDING_HEIGHT` (1.8). No game mode or body definition selects between
them.

Offsets and heights vary per body visual. They belong to body definitions, not to
a shared constant class. The type name matches the utility grab-bag smell the
review rules reject.

## Acceptance criteria

- [ ] Movement tuning and interaction reach live with the simulation and
      interaction code that read them, or in config per 0054.
- [ ] `HOTBAR_SLOT_COUNT` lives with the inventory code.
- [ ] Character height, eye offset, and collider geometry come from the body
      definition, including the standing/flying distinction.
- [ ] The physics and collider factories live with the builders that call them.
- [ ] No replacement "Defaults", "Helper", or "Util" type.

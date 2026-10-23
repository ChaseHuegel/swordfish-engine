# Improvement: Move character tunables into config

- Type: improvement
- Status: open
- Workflow: ../specs/issues.md

## Problem

`WaywardBeyond.Gameplay/CharacterDefaults.cs` hardcodes every character tunable:
`BASE_SPEED`, `DECELERATION`, `ANGULAR_DECELERATION`, `DEFAULT_REACH`,
`HOTBAR_SLOT_COUNT`, and the character height and offset constants. The
development rules require a new tunable to be a TOML config key, and require the
config schema table to change with it. Neither happened.

The shared simulation step reads these values on the client and on the server. A
hardcoded constant cannot differ per embedding, cannot be tuned without a
rebuild, and cannot be validated at load time. `SimulationStep` also divides by
`IPhysics.FixedTimestamp`, so a per-side physics config mismatch is already a
silent prediction desync. One config surface makes that mismatch detectable.

## Acceptance criteria

- [ ] `gameplay.toml` carries movement speed, linear damping, angular damping,
      interaction reach, and hotbar slot count.
- [ ] `WaywardBeyond.Config/GameplayConfig.cs` exposes them as `DataBinding<T>`
      values and validates ranges at load.
- [ ] `CharacterDefaults` no longer declares those constants.
- [ ] Client and server read the keys through the same composition entry.
- [ ] `docs/specs/config-schemas.md` lists every new key with default and range.
- [ ] A test pins that the shared step uses the configured values.

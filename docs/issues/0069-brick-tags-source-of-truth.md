# Improvement: Make one list decide each brick property

- Type: improvement
- Status: open
- Workflow: ../specs/issues.md

## Problem

Two representations describe which bricks share a property.

- `BrickDefinition.Tags` in `WaywardBeyond.Bricks/assets/bricks/*.toml` feeds
  `Brick.LightSource`, `Brick.Entity`, and `Brick.IsOrientable`, and four client
  call sites that test for `environment` and `thruster`.
- The tag lists in `WaywardBeyond.Skills/assets/lang/tags/*.toml` feed the
  authoritative skill XP tables.

The two already disagree. `buildable` lists `grate`, `ice`, `porthole`, `rock`,
`truss`, and `vent`, and none of those bricks carries `buildable` on the brick.
`manufactured` is not a brick tag at all. Adding a brick needs edits in up to
three files across two modules.

The intended direction is to remove tags from the brick definitions and keep one
list per property.

## Acceptance criteria

- [ ] `light`, `entity`, `orientable`, `environment`, and `thruster` each have
      one owner: an explicit brick field, or one tag list in the tag database.
- [ ] `Brick.Tags`, `BrickDefinition.Tags`, and the `Tags` arrays in the brick
      tomls are deleted.
- [ ] `PlayerInteractionService`, `ClientVoxelReconcileSystem`, and
      `ThrusterEntityDecorator` resolve their membership checks without brick
      tags.
- [ ] The skill XP tag lists and the brick definitions agree, or the skill lists
      derive from brick data through `IBrickDatabase`.
- [ ] Tests pin the behaviour that brick tags provided today: light, entity,
      orientable, environment, and thruster.
- [ ] The brick identity and asset definition specs describe the single owner.

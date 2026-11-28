# Survival, Vitals, and Damage

One subject: Adventure Mode health and oxygen simulation, consumable use, active
buff regen, and the single server-side damage path.

## Model

Adventure Mode players carry fixed-point vitals in the replicated
`StatisticsComponent` (`WaywardBeyond.Data/Components/StatisticsComponent.cs`):
`wb:health`, `wb:health_max`, `wb:oxygen`, `wb:oxygen_max`, and the environmental
flag `wb:pressurized` (`0` vacuum, `1` pressurized). `CharacterStatisticsSeeder`
(`WaywardBeyond.Gameplay/Statistics/CharacterStatisticsSeeder.cs`) seeds them at
join from `GameplayConfig.Survival.MaxHealth` and `MaxOxygen`, clamps persisted
values, drops client-forged maximums and pressure, and restores persisted buff
timers. Creative Mode seeds full vitals.

`ServerSurvivalSystem` (`WaywardBeyond.Server/Systems/ServerSurvivalSystem.cs`)
runs in the server world tick for player mirrors that carry
`OwnedCharacterComponent` and `GameModeComponent`:

| Mode | Behavior |
|---|---|
| `Adventure` | drains oxygen in vacuum, refills it in a pressurized room, applies suffocation damage at zero oxygen, runs buff regen, prunes expired buffs, clamps vitals |
| `Creative` | bypasses the simulation and keeps health and oxygen at their maximums |

`Pressurized` defaults to vacuum. A room becomes pressurized only when a future
system writes `wb:pressurized = 1` (epic issue #939).

## Rates

All base rates live in `GameplayConfig.Survival`
(`WaywardBeyond.Config/SurvivalConfig.cs`) and appear in
[config-schemas](config-schemas.md).

| Key | Default | Effect |
|---|---|---|
| `MaxHealth` | `100` | default maximum health for a new Adventure character |
| `MaxOxygen` | `100` | default maximum oxygen for a new Adventure character |
| `OxygenDrainPerSecond` | `2` | vacuum drain |
| `OxygenRefillPerSecond` | `20` | pressurized refill |
| `SuffocationDamagePerSecond` | `5` | damage while oxygen stays at zero |
| `UpdateIntervalSeconds` | `0.25` | continuous write cadence, clamped to at least `0.05` |

The simulation accumulates elapsed seconds per player and writes only when the
cadence is reached, so a `60 Hz` tick does not replicate a tiny oxygen delta.
Damage and consumable use write immediately. Buff regen writes on the cadence.

## Buffs

Active buffs are recorded as `wb:buff.<buffId>.expires_tick` entries in
`StatisticsComponent`. `ServerSurvivalSystem` prunes every entry whose expiry is
at or before the current sim tick, then applies each active buff's
`[[Buffs.RegenRates]]` rows for the elapsed interval. Health and oxygen regen
clamps to the vital maximums.

`Buff.StackPolicy` (`Refresh`, `Extend`, `Ignore`) decides the next expiry tick
when a consumable grants a buff that is already active
(`WaywardBeyond.Data/Buffs/Buff.cs`).

## Consumables

An item opts in with `[Items.Consumable]` (`WaywardBeyond.Items/ConsumableDefinition.cs`):
`BuffID`, `CooldownSeconds`, and `[[Items.Consumable.Modifiers]]`. A modifier row
sets `StatId` plus `FlatDelta` or `PercentDelta`. The secondary input produces
`SecondaryPressed` for a held consumable. The client stages a hintless
interaction event and does not predict the effect.

`ConsumeItemInteractionHandler`
(`WaywardBeyond.Server/Interactions/ConsumeItemInteractionHandler.cs`) runs at the
`React` stage with a `SecondaryPressed` and `Adventure` filter. It:

1. resolves the held item and its consumable block;
2. verifies the active hotbar slot still holds that item;
3. rejects the use while `wb:cooldown.<itemId>` is ahead of the current sim tick;
4. projects the one-shot modifiers in declaration order and refuses to consume
   when no statistic changes and no buff would apply;
5. deducts one unit, writes the projected statistics, applies the buff, and
   writes the cooldown tick.

Modifiers apply as `(current + FlatDelta) * (1 + PercentDelta)`. Health and
oxygen clamp to their maximums. Any statistic id can be targeted, so a
consumable can affect vitals, attributes, or mod statistics.

`ServerSurvivalSystem` removes a cooldown entry once its tick is reached, so
transient keys do not accumulate. Creative Mode does not claim the use. See
[interaction-pipeline](interaction-pipeline.md) for stage composition.

## Damage

`DamageService.ApplyDamage(store, playerEntity, amount, DamageSource, simTick)`
(`WaywardBeyond.Gameplay/Survival/DamageService.cs`) is the only health-reduction
path. Suffocation, kinetic collisions (#933), and later hazards call it. It:

- ignores non-positive or non-finite amounts;
- ignores Creative entities;
- applies kinetic resistance from `wb:kinetic_resist` to `DamageSource.KineticImpact`;
- clamps health to `[0, wb:health_max]`;
- records `DamageStateComponent.LastSource`, `LastAmount`, and `LastTick`;
- records `DeathCause` and `DeathTick` when health reaches zero.

`DamageStateComponent` (`WaywardBeyond.Data/Components/DamageStateComponent.cs`)
is server-side and is not replicated. Death handling consumes the fatal record in
#935.

## Collision damage

`ServerCollisionDamageSystem`
(`WaywardBeyond.Server/Systems/ServerCollisionDamageSystem.cs`) runs after the
world's physics slot and before `ServerSurvivalSystem`. It subscribes to
`IServerWorldPhysics.ContactResolved`, which `ServerPhysicsSystem` forwards from
the engine's Jolt contact listener (see [physics](physics.md)). Contacts are
queued on the tick thread and drained in `Tick`, so the damage calculation stays
in deterministic system order and never runs inside the solver.

For each `Added` contact it evaluates both bodies. A body receives damage only
when all of these hold:

1. it carries `OwnedCharacterComponent`;
2. its `GameModeComponent` is `Adventure`;
3. its `ImpactSpeed` along the contact normal exceeds `CollisionSafeSpeed`;
4. its owner is outside the `CollisionCooldownSeconds` window.

Damage is `min(CollisionMaxDamage, (ImpactSpeed - CollisionSafeSpeed) *
CollisionDamagePerSpeed)` and routes through `DamageService.ApplyDamage` with
`DamageSource.KineticImpact`, so `wb:kinetic_resist` from wearables, augments,
and S.P.A.C.E.R. Resilience mitigates it. A compound voxel manifold reports
several contacts for one slam, and the per-owner cooldown collapses them to one
hit. Creative characters take no damage and record no damage state.

The defaults keep Starter Lifeboat bumps and jetpack walking harmless: a `6 m/s`
closing speed is free, a `10 m/s` slam deals `20`, and a `26 m/s` slam is lethal
to an unarmored `100` health character.

| Key | Default | Effect |
|---|---|---|
| `CollisionSafeSpeed` | `6` | free closing impact speed in meters per second |
| `CollisionDamagePerSpeed` | `5` | damage per meter per second above the safe speed |
| `CollisionMaxDamage` | `100` | per-collision damage cap before resistance |
| `CollisionCooldownSeconds` | `0.25` | per-owner debounce window; `0` disables it |

The four keys are top-level `GameplayConfig` properties and appear in
[config-schemas](config-schemas.md).

## Source of truth

- `WaywardBeyond.Config/SurvivalConfig.cs`, `GameplayConfig.cs`
- `WaywardBeyond.Server/Systems/{ServerSurvivalSystem,ServerCollisionDamageSystem,ServerPhysicsSystem}.cs`
- `WaywardBeyond.Gameplay/Survival/{DamageService,DamageResult}.cs`
- `WaywardBeyond.Data/DamageSource.cs`
- `WaywardBeyond.Data/Components/{DamageStateComponent,StatisticsComponent,StatisticsComponentExtensions}.cs`
- `WaywardBeyond.Data/Statistics/{StatisticIds,StatisticsPersistence}.cs`
- `WaywardBeyond.Data/Buffs/{Buff,BuffRegenRate,IBuffDatabase}.cs`
- `WaywardBeyond.Items/{ConsumableDefinition,ConsumableStatModifierDefinition,ItemDefinition,Item,ItemDatabase}.cs`
- `WaywardBeyond.Server/Interactions/ConsumeItemInteractionHandler.cs`
- `WaywardBeyond.Client/Systems/{InteractionIntent,PlayerInteractionService}.cs`
- `WaywardBeyond.Gameplay/Interactions/InteractionContext.cs`
- `WaywardBeyond.Server/Systems/{ServerJoinSystem,ServerInteractionSystem}.cs`
- `WaywardBeyond.Server/ServerComposition.cs`

## Tests that pin this

- `WaywardBeyond.Tests/SurvivalSystemTests.cs` pins vacuum drain, pressurized
  refill, suffocation damage and death cause, Creative bypass, write cadence,
  buff regen and expiry pruning, consumable parsing and use, cooldown, and the
  damage path.
- `WaywardBeyond.Tests/CollisionDamageSystemTests.cs` pins safe-speed gating,
  scaled and capped damage, compound-contact debouncing, cooldown expiry,
  Creative immunity, kinetic resistance, and contact-side filtering.
- `WaywardBeyond.Tests/InteractionIntentTests.cs` pins the secondary edge for a
  consumable.
- `WaywardBeyond.Tests/ServerJoinStatisticsTests.cs` pins join seeding and buff
  persistence.
- `WaywardBeyond.Tests/StatisticsComponentTests.cs` pins buff timer storage,
  stacking, and pruning.

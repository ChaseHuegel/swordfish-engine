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
| `DeathContainerBlueprintId` | `"builtin/death_container"` | bundled 1-brick blueprint ID spawned on Adventure Mode death |

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
- applies kinetic resistance from `wb:kinetic_resist` to `DamageSource.Kinetic`;
- clamps health to `[0, wb:health_max]`;
- records `DamageStateComponent.LastSource`, `LastAmount`, and `LastTick`;
- records `DeathCause` and `DeathTick` when health reaches zero.

`DamageStateComponent` (`WaywardBeyond.Data/Components/DamageStateComponent.cs`)
is server-side and is not replicated.

## Adventure death, Death-Container drop, and respawn

`ServerDeathSystem` (`WaywardBeyond.Server/Systems/ServerDeathSystem.cs`) runs on the
server world tick directly after `ServerSurvivalSystem` and before
`ServerInteractionSystem`. It drains inbound `Respawn` RPC calls first, then hands every
alive Adventure Mode player with `wb:health <= 0` to `ServerDeathService`
(`WaywardBeyond.Server/Systems/ServerDeathService.cs`). Creative Mode players are immune
to death.

When a player transitions to dead, `ServerDeathService.HandleDeathIfNeeded`:

1. Zeroes velocity and angular velocity, clears staged inputs, and detaches active brick-entity viewers.
2. Collects all non-empty items from `wb:hotbar` and `wb:backpack` while leaving `wb:wearables` and `wb:augments` equipped.
3. Spawns the bundled 1-brick `wb:storage` blueprint configured by `SurvivalConfig.DeathContainerBlueprintId` (default `WaywardBeyond.Server/assets/blueprints/builtin/death_container.wbp`) at the death position, nudging radially if the spawn overlaps solid world colliders.
4. Resizes the container's `wb:cargo` group if dropped stacks exceed 27, populates all dropped items, sets `MachineStateComponent.StationTag = "death_container"` and `MachineStatusFlags.AutoDespawnWhenEmpty`, strips `wb:hotbar` and `wb:backpack` from the player atomically, broadcasts `VoxelEntitySpawn`, and queues `LevelSaveService.QueueSaveStructure`. If container spawning fails, the player keeps all items.
5. Clears active buffs and writes replicated death metadata into `StatisticsComponent`: `wb:dead = 1`, `wb:death_cause` (`(long)DamageSource`), `wb:death_x`, `wb:death_y`, `wb:death_z` (fixed-point coordinates), and `wb:death_has_container` (`1` when a container spawned, `0` otherwise).
6. Queues `LevelSaveService.QueueSaveCharacterLocation` so the dead state persists immediately in `level.db`.

While `StatisticsComponent.IsDead` is true:

- `SimulationStep` skips movement and look commands and damps physics.
- `ServerSurvivalSystem` pauses oxygen drain, refill, suffocation, and regen.
- `ServerInteractionSystem`, `ServerInventorySystem`, `ServerRegionSystem`, `ServerLaserBeamSystem`, `ServerPaintSprayerSystem`, `ConsumeItemInteractionHandler`, and `ServerBrickEntityService` viewer access all reject or suppress actions.

When the last item leaves a `Death-Container` via `ServerInventorySystem` (or on sweep in `ServerBrickEntitySystem`, or when broken in `ServerInteractionSystem`), `ServerBrickEntityService.TryDespawnEmptyAutoDespawnContainer` despawns the brick-entity and its parent `VoxelEntity`, detaches viewers, requests client despawn, and queues `LevelSaveService.QueueDeleteStructure`.

A dead player sends the `Respawn` RPC (`GameRpc.Methods.RESPAWN = 64`, `RespawnRequest` → `RespawnResponse`). `ServerDeathService.RespawnPlayer` rejects alive players (`notification.respawn.notDead`). For dead players it teleports the mirror to `Level.Spawn` with `Quaternion.Identity`, zeroes velocity and torque, clears staged inputs, calls `SimulationStep.ForgetLookHistory`, restores `wb:health` and `wb:oxygen` to their maximums, clears `wb:dead` and `wb:death_*` stats, clears `DamageStateComponent.DeathCause`, and queues a character location save. On the client, `ClientReconcileSystem` detects the `wb:dead` transition from `true` to `false` and calls `LocalPlayerReconciler.SnapToTarget` so the camera and position snap immediately without interpolating across the map.

## Source of truth

- `WaywardBeyond.Config/SurvivalConfig.cs`, `GameplayConfig.cs`
- `WaywardBeyond.Server/Systems/{ServerSurvivalSystem,ServerDeathService,ServerDeathSystem,ServerVoxelEntityBroadcaster}.cs`
- `WaywardBeyond.Server/assets/blueprints/builtin/death_container.wbp`
- `WaywardBeyond.Gameplay/Survival/{DamageService,DamageResult}.cs`
- `WaywardBeyond.Gameplay/SimulationStep.cs`
- `WaywardBeyond.Data/DamageSource.cs`
- `WaywardBeyond.Data/Components/{DamageStateComponent,MachineStateComponent,MachineStatusFlags,StatisticsComponent,StatisticsComponentExtensions}.cs`
- `WaywardBeyond.Data/Statistics/{StatisticIds,StatisticsPersistence}.cs`
- `WaywardBeyond.Data/Buffs/{Buff,BuffRegenRate,IBuffDatabase}.cs`
- `WaywardBeyond.Items/{ConsumableDefinition,ConsumableStatModifierDefinition,ItemDefinition,Item,ItemDatabase}.cs`
- `WaywardBeyond.Server/Interactions/ConsumeItemInteractionHandler.cs`
- `WaywardBeyond.Client/Player/LocalPlayerReconciler.cs`
- `WaywardBeyond.Client/Systems/{ClientReconcileSystem,InteractionIntent,PlayerInteractionService}.cs`
- `WaywardBeyond.Gameplay/Interactions/InteractionContext.cs`
- `WaywardBeyond.Server/Systems/{ServerJoinSystem,ServerInteractionSystem,ServerInventorySystem,ServerBrickEntityService,ServerBrickEntitySystem,ServerRegionSystem,ServerLaserBeamSystem,ServerPaintSprayerSystem}.cs`
- `WaywardBeyond.Server/ServerComposition.cs`

## Tests that pin this

- `WaywardBeyond.Tests/SurvivalSystemTests.cs` pins vacuum drain, pressurized
  refill, suffocation damage and death cause, Creative bypass, write cadence,
  buff regen and expiry pruning, consumable parsing and use, cooldown, and the
  damage path.
- `WaywardBeyond.Tests/DeathAndRespawnTests.cs` pins Adventure death freeze,
  buff clearing, Death-Container blueprint drop and radial nudge, wearables and
  augments retention, auto-despawn across server, `level.db`, and clients,
  `Respawn` RPC rejection and state reset, client reconcile snap, disconnect-while-dead
  anti-duplication, and Creative death immunity.
- `WaywardBeyond.Tests/InteractionIntentTests.cs` pins the secondary edge for a
  consumable.
- `WaywardBeyond.Tests/ServerJoinStatisticsTests.cs` pins join seeding and buff
  persistence.
- `WaywardBeyond.Tests/StatisticsComponentTests.cs` pins buff timer storage,
  stacking, and pruning.

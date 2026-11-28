# Statistics and Modifiers

One subject: how bricks, items, characters, entities, skills, and mods declare numeric statistics and combine them through one extensible modifier pipeline.

## Model

Bricks, items, and entities all carry string-keyed numeric statistics.

- **Bricks** declare optional `[Bricks.Statistics]` key-value pairs in TOML (`BrickDefinition.Statistics` at `WaywardBeyond.Bricks/BrickDefinition.cs:39`, `Brick.Statistics` at `WaywardBeyond.Bricks/Brick.cs:29`). `BrickDatabase.ResolveStatistics` (`WaywardBeyond.Bricks/BrickDatabase.cs:137`) skips empty keys and non-finite values with a warning log.
- **Items** declare optional `[Items.Statistics]` key-value pairs in TOML (`ItemDefinition.Statistics` at `WaywardBeyond.Items/ItemDefinition.cs:21`, `Item.Statistics` at `WaywardBeyond.Items/Item.cs:60`). `ItemDatabase.ResolveStatistics` (`WaywardBeyond.Items/ItemDatabase.cs:126`) skips empty keys and non-finite values with a warning log.
- **Characters and entities** carry key-value statistics on saved `Character.Statistics` (`WaywardBeyond.Data/Nsd/saves.nsd:61`) and on runtime ECS components `StatisticsComponent` (`WaywardBeyond.Data/Components/StatisticsComponent.cs:9`, replicated `OwnerOnly` from server to owning client) and `SkillStateComponent` (`WaywardBeyond.Gameplay/Statistics/SkillStateComponent.cs:10`). `CharacterStatisticsSeeder` (`WaywardBeyond.Gameplay/Statistics/CharacterStatisticsSeeder.cs:15`) seeds `SkillStateComponent` identically on server (`ServerJoinSystem.cs:309`) and client (`PlayerCharacterEntityBuilder.cs:48`), seeds the client's pre-sync `StatisticsComponent` (`PlayerCharacterEntityBuilder.cs:49`), and seeds the server-authoritative `StatisticsComponent` with bounds, defaults, and per-level persistence (`ServerJoinSystem.cs:310`, `CharacterStatisticsSeeder.cs:194`). `ClientNotificationSystem` (`WaywardBeyond.Client/Systems/ClientNotificationSystem.cs:61`) keeps the client player's `SkillStateComponent` in sync with authoritative `SkillStateUpdateMessage` updates.
- **Well-known identifiers** live in `StatisticIds` (`WaywardBeyond.Data/Statistics/StatisticIds.cs:7`), covering `wb:hardness` (default `1.0`), `wb:mining_speed` (default `1.0`), vitals, S.P.A.C.E.R. attributes, environmental flags, fixed-point scaling, and buff timers. Mods define their own namespaced keys without changing engine or game source.

## Evaluation formula

`IStatResolver.Evaluate(in StatQuery query)` (`WaywardBeyond.Gameplay/Statistics/IStatResolver.cs:11`) runs every matching `IStatModifierSource` in priority order against a stack-allocated `StatAccumulator` (`WaywardBeyond.Gameplay/Statistics/StatAccumulator.cs:8`). Evaluation allocates nothing on the heap. `StatQuery.Subject` (`StatSubject` at `WaywardBeyond.Gameplay/Statistics/StatSubject.cs:4`) declares whether the base value belongs to a `Brick`, an `Item`, an `Entity`, or `Any`, so context fields such as `TargetBrickDataId` or `HeldItem` can inform secondary modifier sources without colliding on `SetBase`.

```text
effective = (base + sum(AddBase)) * (1 + sum(AddPercent)) * product(Multiply)
```

| Operation | Effect |
|---|---|
| `SetBase` | Replaces the caller's fallback `DefaultBase` before additive and multiplicative terms apply |
| `AddBase` | Adds a flat offset to the base value |
| `AddPercent` | Adds to the summed percentage scale applied to the base total |
| `Multiply` | Multiplies the post-percentage total independently |

Non-finite modifier values are ignored. A non-finite resolved total falls back to `DefaultBase`.

Health and oxygen are fixed-point vitals stored in `StatisticsComponent`. `ServerSurvivalSystem` writes their base values and active buff regen, and `DamageService` owns the health-reduction path. Expiry entries use `wb:buff.<id>.expires_tick` and per-item cooldowns use `wb:cooldown.<itemId>`. See [survival](survival.md).

## Stat aggregation and S.P.A.C.E.R. scaling

`ServerStatAggregationSystem` (`WaywardBeyond.Server/Systems/ServerStatAggregationSystem.cs:23`) maintains baseline player stats in server-only `BaseStatisticsComponent` (`WaywardBeyond.Data/Components/BaseStatisticsComponent.cs:8`) and persists base values across level saves under `wb:base.<statId>` (`StatisticIds.BaseStat` at `WaywardBeyond.Data/Statistics/StatisticIds.cs:166`) so equip/rejoin cycles do not double-count modifiers.

Recomputation runs only on join or when dirty `InventoryComponent` (`wb:wearables` / `wb:augments` hash) or `StatisticsComponent` (active buff hash / expiry tick) changes occur. Each pass evaluates deterministic layering:

```text
Base + S.P.A.C.E.R. Derived Bonuses + Wearable Modifiers + Augment Modifiers + Active Buff Modifiers
```

1. **S.P.A.C.E.R. pass**: evaluates `wb:strength`, `wb:precision`, `wb:awareness`, `wb:charisma`, `wb:education`, and `wb:resolve` across Base, `wb:wearables`, `wb:augments`, and active `Buff.StatModifiers`.
2. **Derived pass**: adds `SpacerScaling` (`WaywardBeyond.Gameplay/Statistics/SpacerScaling.cs:7`) bonuses per point above `StatisticIds.MIN_ATTRIBUTE` (`wb:strength` and `wb:resolve` → `+5` `wb:health_max`, `wb:resolve` → `+0.02` `wb:kinetic_resist`, `wb:awareness` → `+5` `wb:oxygen_max`, `wb:precision` → `+0.02` `wb:move_speed_pct`, `wb:education` → `+0.02` `wb:mining_speed`) before applying wearable, augment, and buff modifiers.
3. **Clamping**: bounds `wb:health_max`, `wb:oxygen_max`, `wb:kinetic_resist` (`[0, 1]`), and `wb:move_speed_pct`, then clamps current `wb:health` and `wb:oxygen` to the new maximums without granting free heals.

`SimulationStep` (`WaywardBeyond.Gameplay/SimulationStep.cs:232`) reads replicated `wb:move_speed_pct` from `StatisticsComponent` on both client and server and clamps the speed multiplier to `[CharacterDefaults.MIN_MOVE_SPEED_MULTIPLIER, CharacterDefaults.MAX_MOVE_SPEED_MULTIPLIER]` (`0.25` to `2.5`) for prediction parity.

## Modifier sources and priority

`StatResolver` (`WaywardBeyond.Gameplay/Statistics/StatResolver.cs:7`) sorts registered `IStatModifierSource` implementations once at startup by `StatModifierPriority` (`High`, `Normal`, `Low`) and then DI registration order. A source sets `StatId` to target one statistic, or returns `null` to inspect every query.

| Source | Priority | Filter | Behavior |
|---|---|---|---|
| `BrickStatisticModifierSource` | `High` | Any (`null`), `StatSubject.Any` or `Brick` | Reads `Brick.Statistics[query.StatId]` and applies `SetBase` |
| `ItemStatisticModifierSource` | `High` | Any (`null`), `StatSubject.Any` or `Item` | Reads `Item.Statistics[query.StatId]` and applies `SetBase` |
| `EntityStatisticModifierSource` | `Normal` | Any (`null`) | Reads values from `StatisticsComponent` on `query.Store`/`query.Entity` (unscaled for `StatisticIds.IsIntegerStat`, fixed-point via `TryGetFixed` otherwise, skipping buff timer keys) and applies `AddBase` |
| `SkillMiningSpeedModifierSource` | `Normal` | `wb:mining_speed` | Reads `SkillStateComponent` from `query.Store`/`query.Entity` and adds `+2%` (`AddPercent`) per level of the highest `XPSource.Break` skill for `TargetBrickDataId` |

Both `ServerComposition` (`WaywardBeyond.Server/ServerComposition.cs:137`) and `WaywardBeyond.Client/Injector.cs:341` call `container.RegisterStatistics()` (`WaywardBeyond.Gameplay/Statistics/StatisticsComposition.cs:17`) to register the built-in sources and `IStatResolver` idempotently. Game modules and mods register additional modifier sources with `container.RegisterStatModifierSource<T>()` (`StatisticsComposition.cs:33`). `InteractionHandlerEntryPoint` (`WaywardBeyond.Server/Interactions/InteractionHandlerEntryPoint.cs:13`) resolves `IStatResolver` after all modules load so late-loaded mods participate.

## Source of truth

- `WaywardBeyond.Data/Statistics/{StatisticIds,StatisticsPersistence}.cs`
- `WaywardBeyond.Data/Components/{StatisticsComponent,StatisticsComponentExtensions,BaseStatisticsComponent}.cs`
- `WaywardBeyond.Bricks/{BrickDefinition,Brick,BrickDatabase}.cs`
- `WaywardBeyond.Items/{ItemDefinition,Item,ItemDatabase,EquipDefinition,EquipStatModifierDefinition}.cs`
- `WaywardBeyond.Gameplay/Statistics/{StatSubject,StatModifierOperation,StatModifierPriority,StatModifier,StatAccumulator,StatQuery,IStatModifierSource,IStatResolver,StatResolver,SkillStateComponent,CharacterStatisticsSeeder,SpacerScaling,StatisticsComposition}.cs`
- `WaywardBeyond.Gameplay/Statistics/{BrickStatisticModifierSource,ItemStatisticModifierSource,EntityStatisticModifierSource,SkillMiningSpeedModifierSource}.cs`
- `WaywardBeyond.Gameplay/{SimulationStep,CharacterDefaults}.cs`
- `WaywardBeyond.Gameplay/Survival/DamageService.cs`, `WaywardBeyond.Server/Systems/{ServerSurvivalSystem,ServerStatAggregationSystem}.cs`
- `WaywardBeyond.Server/ServerComposition.cs:134,143`, `WaywardBeyond.Server/Systems/ServerJoinSystem.cs:309-310`, `WaywardBeyond.Client/Voxels/Building/PlayerCharacterEntityBuilder.cs:48-49`, `WaywardBeyond.Client/Systems/ClientNotificationSystem.cs:61`, `WaywardBeyond.Client/Injector.cs:341`

## Tests that pin this

`WaywardBeyond.Tests/TimedBrickBreakingTests.cs` pins `[Bricks.Statistics]` and `[Items.Statistics]` loading, non-finite filtering, multi-source composition across brick, item, entity, skill, and custom mod modifier sources, and custom statistic evaluation. `WaywardBeyond.Tests/StatAggregationTests.cs` pins `ServerStatAggregationSystem`, `SpacerScaling`, vital clamping, kinetic mitigation, movement speed prediction parity, and leave/rejoin base persistence. `WaywardBeyond.AssetTools.Tests/Validation/ValidationEngineTests.cs` pins `STAT001` validation for both bricks and items. `WaywardBeyond.AssetStudio.Tests/StudioInteractionTests.cs` pins Asset Studio editing, undo and redo, and lossless TOML persistence for `[Bricks.Statistics]` and `[Items.Statistics]`.

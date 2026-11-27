# Statistics and Modifiers

One subject: how bricks, items, characters, entities, skills, and mods declare numeric statistics and combine them through one extensible modifier pipeline.

## Model

Bricks, items, and entities all carry string-keyed numeric statistics.

- **Bricks** declare optional `[Bricks.Statistics]` key-value pairs in TOML (`BrickDefinition.Statistics` at `WaywardBeyond.Bricks/BrickDefinition.cs:39`, `Brick.Statistics` at `WaywardBeyond.Bricks/Brick.cs:29`). `BrickDatabase.ResolveStatistics` (`WaywardBeyond.Bricks/BrickDatabase.cs:137`) skips empty keys and non-finite values with a warning log.
- **Items** declare optional `[Items.Statistics]` key-value pairs in TOML (`ItemDefinition.Statistics` at `WaywardBeyond.Items/ItemDefinition.cs:19`, `Item.Statistics` at `WaywardBeyond.Items/Item.cs:53`). `ItemDatabase.ResolveStatistics` (`WaywardBeyond.Items/ItemDatabase.cs:59`) skips empty keys and non-finite values with a warning log.
- **Characters and entities** carry key-value statistics on saved `Character.Statistics` (`WaywardBeyond.Data/Nsd/saves.nsd:68`) and on runtime ECS components `StatisticsComponent` (`WaywardBeyond.Gameplay/Statistics/StatisticsComponent.cs:10`) and `SkillStateComponent` (`WaywardBeyond.Gameplay/Statistics/SkillStateComponent.cs:10`). `CharacterStatisticsSeeder` (`WaywardBeyond.Gameplay/Statistics/CharacterStatisticsSeeder.cs:13`) seeds both components onto the player entity identically on the server (`ServerJoinSystem.cs:304`) and the client (`PlayerCharacterEntityBuilder.cs:37`), and `ClientNotificationSystem` (`WaywardBeyond.Client/Systems/ClientNotificationSystem.cs:61`) keeps the client player's `SkillStateComponent` in sync with authoritative `SkillStateUpdateMessage` updates.
- **Well-known identifiers** live in `StatisticIds` (`WaywardBeyond.Data/Statistics/StatisticIds.cs:4`), including `wb:hardness` (default `1.0`) and `wb:mining_speed` (default `1.0`). Mods define their own namespaced keys without changing engine or game source.

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

## Modifier sources and priority

`StatResolver` (`WaywardBeyond.Gameplay/Statistics/StatResolver.cs:7`) sorts registered `IStatModifierSource` implementations once at startup by `StatModifierPriority` (`High`, `Normal`, `Low`) and then DI registration order. A source sets `StatId` to target one statistic, or returns `null` to inspect every query.

| Source | Priority | Filter | Behavior |
|---|---|---|---|
| `BrickStatisticModifierSource` | `High` | Any (`null`), `StatSubject.Any` or `Brick` | Reads `Brick.Statistics[query.StatId]` and applies `SetBase` |
| `ItemStatisticModifierSource` | `High` | Any (`null`), `StatSubject.Any` or `Item` | Reads `Item.Statistics[query.StatId]` and applies `SetBase` |
| `EntityStatisticModifierSource` | `Normal` | Any (`null`) | Reads `StatisticsComponent` from `query.Store`/`query.Entity` and applies `AddBase` |
| `SkillMiningSpeedModifierSource` | `Normal` | `wb:mining_speed` | Reads `SkillStateComponent` from `query.Store`/`query.Entity` and adds `+2%` (`AddPercent`) per level of the highest `XPSource.Break` skill for `TargetBrickDataId` |

Both `ServerComposition` (`WaywardBeyond.Server/ServerComposition.cs:123`) and `WaywardBeyond.Client/Injector.cs:341` call `container.RegisterStatistics()` (`WaywardBeyond.Gameplay/Statistics/StatisticsComposition.cs:17`) to register the built-in sources and `IStatResolver` idempotently. Game modules and mods register additional modifier sources with `container.RegisterStatModifierSource<T>()` (`StatisticsComposition.cs:33`). `InteractionHandlerEntryPoint` (`WaywardBeyond.Server/Interactions/InteractionHandlerEntryPoint.cs:13`) resolves `IStatResolver` after all modules load so late-loaded mods participate.

## Source of truth

- `WaywardBeyond.Data/Statistics/StatisticIds.cs`
- `WaywardBeyond.Bricks/{BrickDefinition,Brick,BrickDatabase}.cs`
- `WaywardBeyond.Items/{ItemDefinition,Item,ItemDatabase}.cs`
- `WaywardBeyond.Gameplay/Statistics/{StatSubject,StatModifierOperation,StatModifierPriority,StatModifier,StatAccumulator,StatQuery,IStatModifierSource,IStatResolver,StatResolver,StatisticsComponent,SkillStateComponent,CharacterStatisticsSeeder,StatisticsComposition}.cs`
- `WaywardBeyond.Gameplay/Statistics/{BrickStatisticModifierSource,ItemStatisticModifierSource,EntityStatisticModifierSource,SkillMiningSpeedModifierSource}.cs`
- `WaywardBeyond.Server/ServerComposition.cs:123`, `WaywardBeyond.Server/Systems/ServerJoinSystem.cs:304`, `WaywardBeyond.Client/Voxels/Building/PlayerCharacterEntityBuilder.cs:37`, `WaywardBeyond.Client/Systems/ClientNotificationSystem.cs:61`, `WaywardBeyond.Client/Injector.cs:341`

## Tests that pin this

`WaywardBeyond.Tests/TimedBrickBreakingTests.cs` pins `[Bricks.Statistics]` and `[Items.Statistics]` loading, non-finite filtering, multi-source composition across brick, item, entity, skill, and custom mod modifier sources, and custom statistic evaluation. `WaywardBeyond.AssetTools.Tests/Validation/ValidationEngineTests.cs` pins `STAT001` validation for both bricks and items. `WaywardBeyond.AssetStudio.Tests/StudioInteractionTests.cs` pins Asset Studio editing, undo and redo, and lossless TOML persistence for `[Bricks.Statistics]` and `[Items.Statistics]`.

# Statistics and Modifiers

One subject: how items, characters, entities, skills, and mods declare numeric statistics and combine them through one extensible modifier pipeline.

## Model

Items and entities both carry string-keyed numeric statistics.

- **Items** declare optional `[Items.Statistics]` key-value pairs in TOML (`ItemDefinition.Statistics` at `WaywardBeyond.Items/ItemDefinition.cs:19`, `Item.Statistics` at `WaywardBeyond.Items/Item.cs:53`). `ItemDatabase.ResolveStatistics` (`WaywardBeyond.Items/ItemDatabase.cs:59`) skips empty keys and non-finite values with a warning log.
- **Characters and entities** carry key-value statistics on saved `Character.Statistics` (`WaywardBeyond.Data/Nsd/saves.nsd:68`) and on runtime `StatisticsComponent` (`WaywardBeyond.Gameplay/Statistics/StatisticsComponent.cs:10`). `ServerJoinSystem.SeedStatistics` (`WaywardBeyond.Server/Systems/ServerJoinSystem.cs:394`) seeds non-skill character statistics onto the server player mirror at join.
- **Well-known identifiers** live in `StatisticIds` (`WaywardBeyond.Data/Statistics/StatisticIds.cs:4`), starting with `wb:mining_speed` (default `1.0`). Mods define their own namespaced keys without changing engine or game source.

## Evaluation formula

`IStatResolver.Evaluate(in StatQuery query)` (`WaywardBeyond.Gameplay/Statistics/IStatResolver.cs:11`) runs every matching `IStatModifierSource` in priority order against a stack-allocated `StatAccumulator` (`WaywardBeyond.Gameplay/Statistics/StatAccumulator.cs:8`). Evaluation allocates nothing on the heap.

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
| `ItemStatisticModifierSource` | `High` | Any (`null`) | Reads `Item.Statistics[query.StatId]` and applies `SetBase` |
| `EntityStatisticModifierSource` | `Normal` | Any (`null`) | Reads `StatisticsComponent` (or client `CharacterStatistics`) and applies `AddBase` |
| `SkillMiningSpeedModifierSource` | `Normal` | `wb:mining_speed` | Adds `+2%` (`AddPercent`) per level of the highest `XPSource.Break` skill for `TargetBrickDataId` |

Both `ServerComposition` (`WaywardBeyond.Server/ServerComposition.cs:121`) and `WaywardBeyond.Client/Injector.cs:341` register the built-in sources idempotently and register `StatResolver` as `IStatResolver`. `InteractionHandlerEntryPoint` (`WaywardBeyond.Server/Interactions/InteractionHandlerEntryPoint.cs:13`) resolves `IStatResolver` after all modules load so late-loaded mods participate.

## Source of truth

- `WaywardBeyond.Data/Statistics/StatisticIds.cs`
- `WaywardBeyond.Items/{ItemDefinition,Item,ItemDatabase}.cs`
- `WaywardBeyond.Gameplay/Statistics/{StatModifierOperation,StatModifierPriority,StatModifier,StatAccumulator,StatQuery,IStatModifierSource,IStatResolver,StatResolver,StatisticsComponent}.cs`
- `WaywardBeyond.Gameplay/Statistics/{ItemStatisticModifierSource,EntityStatisticModifierSource,SkillMiningSpeedModifierSource}.cs`
- `WaywardBeyond.Server/ServerComposition.cs:121`, `WaywardBeyond.Server/Systems/ServerJoinSystem.cs:394`, `WaywardBeyond.Client/Injector.cs:341`

## Tests that pin this

`WaywardBeyond.Tests/TimedBrickBreakingTests.cs` pins `[Items.Statistics]` loading, non-finite filtering, multi-source composition across item, entity, skill, and custom mod modifier sources, and custom statistic evaluation. `WaywardBeyond.AssetTools.Tests/Validation/ValidationEngineTests.cs` pins `STAT001` validation. `WaywardBeyond.AssetStudio.Tests/StudioInteractionTests.cs` pins Asset Studio editing, undo and redo, and lossless TOML persistence for `[Items.Statistics]`.

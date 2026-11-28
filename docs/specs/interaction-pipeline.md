# Interaction Pipeline

One subject: how one player interaction becomes a resolved outcome, an authority change,
and its consequences. The shared interaction rules live in
[networking-voxel-edits](networking-voxel-edits.md); this doc owns the handler pipeline
that runs them.

## Model

An interaction moves through five ordered stages. Each stage holds a list of handlers,
sorted by priority band and then registration order. A handler sees a mutable
`InteractionContext` and returns an `InteractionDecision`.

```
Select → Resolve → Validate → Commit → React
```

| Stage | Job | Default handler |
|---|---|---|
| `Select` | Determine the content the interaction operates with | `ItemPlaceableSelector` maps the held item to the brick it places |
| `Resolve` | Derive the base outcome from the client's target hint | `PaintHintResolver` (High) resolves paint hints, `HintResolver` (Normal) runs `InteractionResolver` |
| `Validate` | Adjust or reject before any authority change | `BreakToolValidator` rejects a break made without a removal tool |
| `Commit` | Perform the authority change | `VoxelFaceWriteCommitter` (Normal) applies face overrides, `VoxelWriteCommitter` (Low) writes bricks, rebuilds the collider, and broadcasts |
| `React` | Apply the consequences | `BreakLootReactor`, `PlaceConsumeReactor`, `ConsumeItemInteractionHandler`, `SkillXpReactor` |

The game and its mods register the same kind of handler. There is no privileged path:
a mod can replace the authority write, cancel it, or produce a different outcome entirely.

## Registration

A handler carries its own `Stage` and `Priority` as properties, so the type states where it
belongs. The composition registers the implementation as an `IInteractionHandler`; the
registry is built once at startup from every registration, after all modules have loaded.
A mod registers its implementation and never resolves the registry, so module load order
does not decide placement.

## Priority

Within a stage, handlers run `High` then `Normal` then `Low`; ties keep DI registration
order. The default authority write declares `Low`, so any `High` or `Normal` handler in
`Commit` gets the outcome first. A handler that claims the outcome returns
`InteractionDecision.Handled`, which skips the remaining handlers in the stage — including
the built-in write.

## Decision

| Decision | Effect |
|---|---|
| `Continue` | Run the next handler in the same stage |
| `Handled` | Skip the rest of this stage, continue to the next stage |
| `Consume` | Stop the pipeline; no later stage runs |
| `Cancel` | Stop the pipeline; the interaction is rejected |

`Handled` is stage-scoped ownership. In `Commit` it asserts the outcome is now
authoritative, which is what lets `React` trust the resolution. `Consume` is
pipeline-scoped ownership: a mod that takes over the whole interaction, including
suppressing reactions, returns it from `Commit`.

## Composition rules

- A handler that writes an outcome sets `Handled`. The built-in write then never runs.
- A handler that declines an outcome returns `Continue`, leaving the fallback to apply it.
- A handler that wrote nothing and wants no downstream processing returns `Cancel`.
- `React` handlers gate on `InteractionContext.Resolution.Action`, so a pipeline that
  resolved to `None` reacts to nothing.

## Consumable use

A consumable item binds `SecondaryPressed` in `InteractionIntent` (`WaywardBeyond.Client/Systems/InteractionIntent.cs:15`).
`PlayerInteractionService` stages a hintless secondary event, so no voxel target or prediction is involved.
On the server, `ConsumeItemInteractionHandler` (`WaywardBeyond.Server/Interactions/ConsumeItemInteractionHandler.cs`)
runs at `React` with the `SecondaryPressed` and `Adventure` filter. It resolves the held item's
`[Items.Consumable]` block, checks benefit and cooldown, deducts one unit from the active hotbar slot,
and applies health, oxygen, and buff effects. See [survival](survival.md).

## Client prediction

Prediction is composition, not a mode. The client builds its own registry with the
handlers it wants to predict; today that is the voxel edit only, and no effect is
predicted. The shared `HintResolver` and `InteractionResolver` are already usable
on both sides.

## Threading

The pipeline runs on the world's tick thread. Handlers receive the `DataStore` and must
not touch it from another thread. Handlers are stateless services registered once, so
they must not keep per-world state; it lives in the context or the store.

## Stage slices and timed interactions

`IInteractionHandlerRegistry.Run(ref InteractionContext context, InteractionStage startStage, InteractionStage endStage)`
(`WaywardBeyond.Gameplay/Interactions/IInteractionHandlerRegistry.cs:17`,
`InteractionHandlerRegistry.cs:62`) executes an inclusive slice of stages.

Timed interactions compose through the pipeline rather than living hardcoded inside the pump:

- **Server hold declaration:** Any `Validate` handler can require a sustained hold by setting
  `InteractionContext.Hold` (`WaywardBeyond.Gameplay/Interactions/InteractionContext.cs:96`)
  to an `InteractionHold` (`InteractionHold.cs:7`) with a stable `TimedInteractionKey`,
  `RequiredDuration`, `RequiredInput` (`InteractionInput.Primary`, `Secondary`, or `None` in
  `InteractionInput.cs:4`), and `CancelOnEquipmentChange`. `InteractionInputExtensions`
  (`InteractionInputExtensions.cs:6`) maps `InteractionInput` channels to wire `InteractionKind`
  edges and `InputComponent` held state, failing closed on unrecognized input values.
- **Generic server pump:** `ServerInteractionSystem`
  (`WaywardBeyond.Server/Systems/ServerInteractionSystem.cs:23`) runs `Select` through `Validate`.
  Interactions without `Hold.IsRequired` run `Commit` through `React` immediately. When
  `Hold.IsRequired` is true and the required input is held, `ServerInteractionSystem` opens or
  retains a per-player timed session. Each tick re-validates `Select` through `Validate`, verifies
  the input condition, equipment, and `TimedInteractionKey`, and advances elapsed time. When
  `TimedInteractionProgress.IsComplete` (`TimedInteractionProgress.cs:12`) becomes true, the server
  clears the session and runs `Commit` through `React`.
- **Adventure brick breaking validator:** `AdventureBreakHoldValidator`
  (`WaywardBeyond.Server/Interactions/AdventureBreakHoldValidator.cs:15`) registers at
  `InteractionStage.Validate` with `Priority.Low` and `GameMode.Adventure`. For `Break` actions, it
  evaluates `StatisticIds.HARDNESS` and `StatisticIds.MINING_SPEED` through `IStatResolver` (see
  [statistics](statistics.md)) to compute `hardness / speed` and populate `context.Hold`. Higher
  priority handlers and custom `IStatModifierSource` implementations can modify or replace it.
- **Extensible client holds:** `PlayerInteractionService`
  (`WaywardBeyond.Client/Systems/PlayerInteractionService.cs:254`) routes both immediate and timed
  interactions through `InteractionInput` and delegates timed holds to
  `ClientTimedInteractionController`
  (`WaywardBeyond.Client/Interactions/ClientTimedInteractionController.cs:13`), which runs
  registered `IClientTimedInteraction` providers (`IClientTimedInteraction.cs:10`) keyed by
  logical `InteractionInput`. `AdventureBreakTimedInteraction`
  (`AdventureBreakTimedInteraction.cs:22`) claims Adventure mining holds, suppresses predictive
  voxel removal and repeat chatter, stages one start edge and one cancel edge, and updates
  `InteractionState.HoldProgress` (`WaywardBeyond.Client/Systems/InteractionState.cs:19`) using
  the shared `IStatResolver`. Both `ServerInteractionSystem` and the client interaction services
  cache their `IVoxelInteractionWorld` per `DataStore` to avoid per-tick allocations during holds.

## Source of truth

- `WaywardBeyond.Gameplay/Interactions/{InteractionStage,InteractionPriority,InteractionDecision,InteractionRunResult}.cs`
- `WaywardBeyond.Gameplay/Interactions/{InteractionContext,IInteractionHandler,IInteractionHandlerRegistry,InteractionHandlerRegistry}.cs`
- `WaywardBeyond.Gameplay/Interactions/{InteractionInput,InteractionInputExtensions,InteractionHold,TimedInteractionKey,TimedInteractionProgress,HintResolver}.cs`
- `WaywardBeyond.Client/Interactions/{IClientTimedInteraction,ClientTimedInteractionContext,ClientTimedTarget,ClientTimedStageEvent,ClientTimedInteractionController,AdventureBreakTimedInteraction}.cs`
- `WaywardBeyond.Client/Systems/{InteractionState,PlayerInteractionService}.cs`
- `WaywardBeyond.Server/Interactions/` — default `Select`, `Validate` (`BreakToolValidator`, `AdventureBreakHoldValidator`), `Commit`, and `React` handlers (`BreakLootReactor`, `PlaceConsumeReactor`, `ConsumeItemInteractionHandler`, `SkillXpReactor`)
- `WaywardBeyond.Server/Systems/ServerInteractionSystem.cs` — generic interaction pump and timed session tracker
- `WaywardBeyond.Server/ServerComposition.cs` — default handler and stat modifier registration
- `WaywardBeyond.Tests/TimedBrickBreakingTests.cs` — unit and integration coverage

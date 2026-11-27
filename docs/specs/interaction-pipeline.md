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
| `React` | Apply the consequences | `BreakLootReactor`, `PlaceConsumeReactor`, `SkillXpReactor` |

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

## Client prediction

Prediction is composition, not a mode. The client builds its own registry with the
handlers it wants to predict; today that is the voxel edit only, and no effect is
predicted. The shared `HintResolver` and `InteractionResolver` are already usable
on both sides.

## Threading

The pipeline runs on the world's tick thread. Handlers receive the `DataStore` and must
not touch it from another thread. Handlers are stateless services registered once, so
they must not keep per-world state; it lives in the context or the store.

## Stage slices and Adventure timed breaking

`IInteractionHandlerRegistry.Run(ref InteractionContext context, InteractionStage startStage, InteractionStage endStage)`
(`WaywardBeyond.Gameplay/Interactions/IInteractionHandlerRegistry.cs:17`,
`InteractionHandlerRegistry.cs:62`) lets callers execute an inclusive slice of
stages.

- **Creative mode:** Every interaction runs `Select` through `React` immediately.
- **Adventure placements and paints:** `ServerInteractionSystem`
  (`WaywardBeyond.Server/Systems/ServerInteractionSystem.cs:135`) runs `Select`
  through `Validate` and then immediately runs `Commit` through `React`.
- **Adventure brick breaking:** When `Action == InteractionAction.Break` and
  `InputComponent.PrimaryHeld` is true, `ServerInteractionSystem` opens or keeps
  a per-player `BreakSession` (`TargetEntityUuid`, `TargetCoord`,
  `TargetBrickDataId`, `ActiveSlot`, `HeldItemId`, `Elapsed`,
  `RequiredDuration`) without running `Commit` yet. Each server tick validates the
  active session with `Select` through `Validate` and advances `Elapsed`. Releasing
  primary, sending `InteractionKind.PrimaryReleased`, switching slots or items,
  retargeting, losing `BreakToolValidator`, leaving `CharacterDefaults.DEFAULT_REACH`,
  or mutating the target voxel clears the session and resets progress to `0`.
  When `BreakDuration.IsComplete` becomes true (`ServerInteractionSystem.cs:310`),
  the server clears the session and runs `Commit` through `React` (`VoxelWriteCommitter`,
  `BreakLootReactor`, `SkillXpReactor`).
- **Duration formula:** `BreakDuration.CalculateBreakDuration`
  (`WaywardBeyond.Gameplay/Interactions/BreakDuration.cs:62`) computes
  `hardness / (toolMiningSpeed * skillMultiplier)`, where `skillMultiplier` is
  `1.0 + Math.Max(0, skillLevel) * 0.02` from the highest skill matching
  `XPSource.Break` for that brick. Invalid divisors and negative inputs fall back
  to safe positive defaults.
- **Client progress sync:** `PlayerInteractionService.UpdateAdventureBreakHold`
  (`WaywardBeyond.Client/Systems/PlayerInteractionService.cs:882`) skips
  predictive voxel removal during Adventure holds and sends one `PrimaryPressed`
  when targeting a brick and one `PrimaryReleased` when dropping or releasing it.
  Local `[0..1]` progress updates `InteractionState.BreakProgress`
  (`WaywardBeyond.Client/Systems/InteractionState.cs:19`) using the shared
  `BreakDuration` helper and resets immediately on any cancellation condition.

## Source of truth

- `WaywardBeyond.Gameplay/Interactions/{InteractionStage,InteractionPriority,InteractionDecision,InteractionRunResult}.cs`
- `WaywardBeyond.Gameplay/Interactions/{InteractionContext,IInteractionHandler,IInteractionHandlerRegistry,InteractionHandlerRegistry}.cs`
- `WaywardBeyond.Gameplay/Interactions/{BreakDuration,HintResolver}.cs`
- `WaywardBeyond.Client/Systems/{InteractionState,PlayerInteractionService}.cs` — Adventure client hold progress
- `WaywardBeyond.Server/Interactions/` — the default `Select`, `Commit`, and `React` handlers
- `WaywardBeyond.Server/Systems/ServerInteractionSystem.cs` — the pump and Adventure hold tracker
- `WaywardBeyond.Server/ServerComposition.cs` — default registration order
- `WaywardBeyond.Tests/TimedBrickBreakingTests.cs` — unit and integration coverage

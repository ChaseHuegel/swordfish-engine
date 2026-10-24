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
Select → Resolve → Validate → Apply → React
```

| Stage | Job | Default handler |
|---|---|---|
| `Select` | Determine the content the interaction operates with | none yet |
| `Resolve` | Derive the base outcome from the client's target hint | `HintResolveHandler` runs `InteractionResolver` |
| `Validate` | Adjust or reject before any authority change | none; the stage is a policy hook |
| `Apply` | Perform the authority change | none yet |
| `React` | Apply the consequences | none yet |

The game and its mods register the same kind of handler. There is no privileged path:
a mod can replace the authority write, cancel it, or produce a different outcome entirely.

## Priority

Within a stage, handlers run `High` then `Normal` then `Low`; ties keep registration
order. The default authority write registers `Low`, so any `High` or `Normal` handler in
`Apply` gets the outcome first. A handler that claims the outcome returns
`InteractionDecision.Handled`, which skips the remaining handlers in the stage — including
the built-in write.

## Decision

| Decision | Effect |
|---|---|
| `Continue` | Run the next handler in the same stage |
| `Handled` | Skip the rest of this stage, continue to the next stage |
| `Consume` | Stop the pipeline; no later stage runs |
| `Cancel` | Stop the pipeline; the interaction is rejected |

`Handled` is stage-scoped ownership. In `Apply` it asserts the outcome is now
authoritative, which is what lets `React` trust the resolution. `Consume` is
pipeline-scoped ownership: a mod that takes over the whole interaction, including
suppressing reactions, returns it from `Apply`.

## Composition rules

- A handler that writes an outcome sets `Handled`. The built-in write then never runs.
- A handler that declines an outcome returns `Continue`, leaving the fallback to apply it.
- A handler that wrote nothing and wants no downstream processing returns `Cancel`.
- `React` handlers gate on `InteractionContext.Resolution.Action`, so a pipeline that
  resolved to `None` reacts to nothing.

## Client prediction

Prediction is composition, not a mode. The client builds its own registry with the
handlers it wants to predict; today that is the voxel edit only, and no effect is
predicted. The shared `HintResolveHandler` and `InteractionResolver` are already usable
on both sides.

## Threading

The pipeline runs on the world's tick thread. Handlers receive the `DataStore` and must
not touch it from another thread. Handlers are stateless services registered once, so
they must not keep per-world state; it lives in the context or the store.

## Source of truth

- `WaywardBeyond.Gameplay/Interactions/{InteractionStage,InteractionPriority,InteractionDecision,InteractionRunResult}.cs`
- `WaywardBeyond.Gameplay/Interactions/{InteractionContext,IInteractionHandler,IInteractionHandlerRegistry,InteractionHandlerRegistry}.cs`
- `WaywardBeyond.Gameplay/Interactions/HintResolveHandler.cs`
- `WaywardBeyond.Server/Systems/ServerInteractionSystem.cs` — the pump
- `WaywardBeyond.Server/ServerComposition.cs` — default registration order

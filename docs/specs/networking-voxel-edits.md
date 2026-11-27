# Networking — Server-Authoritative Interactions and Voxel Edits

One subject: how player interactions (place/break) resolve and replicate.

## Model

The server is the **sole authority** for player interaction *outcome*. The
client never mutates authority voxel state; it only predicts presentably. The
client sends intent, not result.

The pipeline: intent upstream → shared resolve + authority apply → downstream
replicate.

## Intent upstream

- **Continuous held state** (`PrimaryHeld`/`SecondaryHeld`) rides the per-frame
  `InputComponent` packet (`WaywardBeyond.Data/Nsd/components.nsd`).
- **Client-authoritative active slot** rides `ClientOwned` `EquipmentComponent`.
- **Discrete button edges** are delivered as a `ClientOwned` `InteractionEvent`
  (uuid 15) — an extensible pseudo-union of nullable hint sub-messages. The
  server stages them into each player mirror's
  `NetworkComponent.StagedInteractions` ring, keyed by `ServerTickAtSample`,
  deduped by `SequenceNumber`, and consumed in tick order.

`InteractionEvent` carries `Entity`, `SequenceNumber`, `ServerTickAtSample`, and
`Kind` at the root. `Kind` is the input edge and is not the union discriminator;
hint presence is. A hint-less event is valid and resolves to `Action.None`. See
[messages](networking-messages.md).

## Client input and hold-to-repeat

`PlayerInteractionService` receives discrete button edges from
`IInputService.Clicked` on the window thread, queues them, and drains the queue
on the ECS thread (`WaywardBeyond.Client/Systems/PlayerInteractionService.cs:266`).
One edge runs one interaction attempt.

The control scheme maps each mouse button to an edge from the held item
(`WaywardBeyond.Client/Systems/InteractionIntent.cs`). The left button is the
primary edge for every item that has a primary action: a modifier tool applies its
paint, a placeable item places its brick, and a removal tool breaks the targeted
brick. The right button is the secondary edge and only a modifier tool binds it,
to clear its paint. Empty hands and items without an action for a button produce
no edge.

Holding primary or secondary in Creative mode, or when placing or painting in
Adventure mode, repeats the attempt on a 0.2 second interval until
release (`WaywardBeyond.Client/Systems/PlayerInteractionService.cs:58`). The
interval starts at the press edge. `InteractionRepeatTimer` owns the timing
(`WaywardBeyond.Client/Systems/InteractionRepeatTimer.cs:4`). The repeat stops on
release, when the game leaves `Playing`, or when an interaction blocker opens.

A repeat is a full attempt. It raycasts again, resolves again, predicts again,
and stages its own `InteractionEvent` with a fresh `SequenceNumber`. A repeat
that is rejected stays silent: only the press edge pushes an invalid-target or
occupied-cell notification
(`WaywardBeyond.Client/Systems/PlayerInteractionService.cs:479`).
`WaywardBeyond.Tests/InteractionRepeatTimerTests.cs` pins the timing.

Timed hold interactions (such as holding primary with `ToolType.Mining` in Adventure mode) are handled by registered `IClientTimedInteraction` providers through `ClientTimedInteractionController` (`WaywardBeyond.Client/Interactions/ClientTimedInteractionController.cs:15`). `AdventureBreakTimedInteraction` (`WaywardBeyond.Client/Interactions/AdventureBreakTimedInteraction.cs:26`) emits one `PrimaryPressed` edge when starting or changing targets, emits one `PrimaryReleased` edge on cancellation or release, skips predictive voxel removal during the hold, and advances `InteractionState.HoldProgress` locally through the shared `IStatResolver` pipeline (see [statistics](statistics.md)).

## Shared resolution

Targeting and outcome logic lives in shared code
(`WaywardBeyond.Gameplay/Interactions`), called identically by client
prediction and server authority:

- `InteractionResolver` takes an `InteractionRequest` (origin, hint, input
  edge, held placeable `Brick`, game mode, reach) plus the caller's
  `IVoxelInteractionWorld` and produces `{ Action None | Break | Place,
  coordinate, voxel }`. A primary press places when the request carries a held
  placeable and breaks the occupied cell otherwise. A release or secondary press
  does not edit bricks.
- Client targeting is a deterministic-from-ray routine
  (`TryGetBrickFromScreenSpace`: surface bias, reach-around probe rays, offset
  march-back). Server validation never raycasts; it validates the hinted
  structure + cell by identity.
- Rules: the brick is in reach when any part of its cell is within reach of
  the player origin. Break requires an occupied cell. Place requires an empty
  destination cell, a held placeable brick that the brick registry knows, a
  shape byte inside `BrickShape`, and no physics collider intersection with
  the resolved brick collider (excluding the target structure itself). This
  prevents players from trapping themselves or overlapping NPCs, monsters, or
  other ships. A hint-less event, or a hint that fails
  reach/occupancy/plausibility/overlap, resolves to `Action.None`. The resolver
  never throws for a missing hint.

## Authority apply

`ServerInteractionSystem` (`WaywardBeyond.Server/Systems/`) is the pump: on the
server tick between the replication Apply and Publish stages it builds the
interaction context and runs the pipeline (see
[interaction-pipeline](interaction-pipeline.md)).

The default handlers:

- `ItemPlaceableSelector` maps the held item to the brick it places.
- `HintResolver` validates the hinted structure and cell against the
  authority store by identity and reach. It never raycasts, and it resolves
  with `InteractionResolver`.
- `BreakToolValidator` rejects a break unless the active slot holds a removal
  tool (`ToolType.Mining`), so bare hands and block items cannot break a brick.
- `VoxelWriteCommitter` applies the outcome on the structure's live `VoxelObject`
  (`WaywardBeyond.Server/Components/VoxelWorldComponent.cs`) and rebuilds the
  structure `ColliderComponent` (`VoxelColliderBuilder`) so subsequent raycasts
  see the change. Saves and streams re-derive `ChunkInfo[]` from the live
  `VoxelObject`; the authority holds no parallel chunk copy.
- `BreakLootReactor` and `PlaceConsumeReactor` grant a broken brick's own item
  and consume one held item on place, against the server-owned
  `InventoryComponent`. Creative is free on both paths.
- `SkillXpReactor` grants the brick's skill XP.

## Downstream replication

Every applied edit broadcasts to **all** world peers as a `VoxelEdit`
notification (method 350) through each peer's cached client proxy. The RPC
transport is ordered and lossless:

```nsd
message VoxelEditMessage { ulong EntityUuid; int X, Y, Z; WaywardBeyond.Data.Voxel Voxel; uint Sequence; string? BrickId; }
```

The `BrickId` carries the canonical brick name so the client reconciles by name
even when its registry ids differ from the server's. See
[brick-identity](brick-identity.md).

### Edit audio

`ClientVoxelReconcileSystem.ApplyAuthoritativeEdit` raises `BrickEvent` for
**unpredicted** edits only: a remote player's mining and construction is audible
to everyone else. Edits correlating to the local player's own pending
predictions (confirm, snap, or revert) raise nothing because the prediction path
already raised `BrickEvent` (`PlayerInteractionService.FirePresentationHook`).
Break vs place is decided by the resulting voxel (empty = break, filled = place).
The brick id comes from the cell before the write for a break (a break's result
is empty). `BrickSoundListener` resolves the brick and plays its
`[Bricks.Sounds]` tags without touching local character statistics. See
[sound-effects](sound-effects.md).

`ClientVoxelReconcileSystem` (`WaywardBeyond.Client/Systems/`, gated on
`Playing`) drains `ClientInbound.VoxelEdits` and correlates each echo against
the local `PendingInteractionComponent.Queue` by
`(entity, coordinate, sequence)`:

- voxel-matching echo → **confirm** (no-op);
- differing echo → **snap** to authority;
- prediction still pending past `REVERT_BOUND_SIM_TICKS` (40) with no echo →
  **revert** (the server rejected it).
- unpredicted edits (remote witnesses) apply directly.

A confirmed or reconciled edit updates localized lighting and marks affected
chunks dirty in `VoxelChunkRegistry`. `VoxelEntityRebuildSystem` rebuilds only
dirty chunks and the surface collider on the ECS thread. It stops after
`VoxelSettings.RebuildBudgetMs` (10 ms by default) and resumes on the next tick.
A light-only chunk skips collision and voxel-entity reconciliation. The rebuild
frees replaced chunk entities and the engine releases their renderers and
batches. See [resource-lifetime](resource-lifetime.md).

## Per-face paint interactions

Holding `wb:sprayer` (`ToolType.Sprayer`, Color mode) or `wb:drill`
(`ToolType.Drill`, Texture mode) routes primary and secondary clicks through
`PaintInteraction` (`WaywardBeyond.Data/Nsd/components.nsd`) and
`PaintInteractionResolver` (`WaywardBeyond.Gameplay/Interactions/PaintInteractionResolver.cs`).
Holding `R` opens `PaintColorSelector` when holding the sprayer, or
`PaintTextureSelector` when holding the drill. The left button applies the
tool's slot. The right button clears only the tool's slot. Middle click acts as
an eye dropper instead of brick pick, selecting the clicked face's painted color
for the sprayer, or the clicked face's painted texture (falling back to the
matching texture paint for that brick face) for the drill.

Validation runs strictly at the interaction layer:
- `PaintHintResolver` (`WaywardBeyond.Server/Interactions/PaintHintResolver.cs`)
  verifies the held item tool matches the requested mode (`ToolType.Sprayer`
  for Color, `ToolType.Drill` for Texture).
- The target brick must carry the `paintable` tag (`Brick.Paintable`).
- Texture paint `Alpha` must match the target brick `Alpha`. Color paint applies
  to any paintable brick.
- `BrickShape.Slope` expands `Front` or `Top` hits to `Front | Top`.
- `BrickShape.Custom` expands any face hit to `BrickFaceMask.All`.
- On the client, `PlayerInteractionService` highlights invalid sprayer or drill
  targets with the invalid outline color. Attempting to paint an invalid target
  or clear a face with no active-mode override shows a red action notification
  and skips sending an interaction event.

`VoxelFaceWriteCommitter` (`WaywardBeyond.Server/Interactions/VoxelFaceWriteCommitter.cs`)
applies the edit to the live `VoxelObject` without rebuilding colliders and
broadcasts `VoxelFaceEditMessage` on RPC method `351` (`VOXEL_FACE_EDIT`),
echoing `Sequence` only to the originating client and `0` to witness peers.
`ClientVoxelReconcileSystem` reconciles echoes by canonical `PaintId` and marks
affected chunks mesh-only dirty via `MarkFaceModified`. `MarkFaceModified` also
flags the edited chunk in `EntityDirtyChunks` so `LightEntityDecorator` updates
light bricks (`Brick.LightSource`) to use the average of their painted face
colors in place of the brick color, without rebuilding colliders.

## Interaction handlers

The pipeline that runs each stage, the priority bands, and the four decisions live in
[interaction-pipeline](interaction-pipeline.md). This section records what the default
handlers do.

Every interaction outcome resolves server-side, and handlers are the extension
point for gameplay, not only for mods.

- A handler declares its stage, priority, and an `InteractionHandlerFilter`
  (`InteractionKind?`/`HeldItemID?`/`GameMode?`, null = match-any), and registers
  as an `IInteractionHandler`. `ServerComposition` registers the built-ins;
  `InteractionHandlerEntryPoint` builds the shared registry from all of them.
- `ServerInteractionSystem` is the pump: it builds the context and hands it to
  the pipeline, which runs the stages in order.
- A handler mutates the running context and returns a decision. `Handled` owns
  the stage, `Consume` owns the interaction, and `Cancel` rejects it.
- Matching handlers run by priority band, then registration order. Each handler
  sees the running resolution, so a change by an earlier handler carries
  forward.
- `High` pre-empts the built-ins; the default authority write registers
  `Low`, so a handler that claims the outcome replaces it.

The interaction context components (`EquipmentComponent`, `InventoryComponent`,
`GameModeComponent`) are seeded from the client save via
`JoinRequest.CharacterState` at join and server-owned thereafter. See
[join](networking-join.md).

## Source of truth

- `WaywardBeyond.Client/Systems/PlayerInteractionService.cs` — click edges, hold-to-repeat dispatch, timed hold delegation, and prediction
- `WaywardBeyond.Client/Interactions/` — extensible `IClientTimedInteraction` providers and `ClientTimedInteractionController`
- `WaywardBeyond.Client/Systems/{InteractionIntent,InteractionRepeatTimer}.cs` — control scheme and repeat timing
- `WaywardBeyond.Gameplay/Interactions/` — resolver, interaction holds, interaction inputs, request, handlers, registry
- `WaywardBeyond.Gameplay/Statistics/` — extensible statistic modifier pipeline
- `WaywardBeyond.Server/Systems/ServerInteractionSystem.cs`
- `WaywardBeyond.Server/Interactions/` — default handlers, including `BreakToolValidator` and `AdventureBreakHoldValidator`
- `WaywardBeyond.Server/Systems/VoxelColliderBuilder.cs`
- `WaywardBeyond.Gameplay/Networking/{NetworkComponent,StageRing}.cs`
  (`StagedInteractions`)

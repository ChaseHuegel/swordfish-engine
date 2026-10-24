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
`Kind` at the root. `Kind` is the button/edge and is not the union discriminator;
hint presence is. A hint-less event is valid and resolves to `Action.None`. See
[messages](networking-messages.md).

## Shared resolution

Targeting and outcome logic lives in shared code
(`WaywardBeyond.Gameplay/Interactions`), called identically by client
prediction and server authority:

- `InteractionResolver` takes an `InteractionRequest` (origin, hint, button
  edge, held placeable `Brick`, game mode, reach) plus the caller's
  `IVoxelInteractionWorld` and produces `{ Action None | Break | Place,
  coordinate, voxel }`.
- Client targeting is a deterministic-from-ray routine
  (`TryGetBrickFromScreenSpace`: surface bias, reach-around probe rays, offset
  march-back). Server validation never raycasts; it validates the hinted
  structure + cell by identity.
- Rules: the brick is in reach when any part of its cell is within reach of
  the player origin. Break requires an occupied cell. Place requires an empty
  destination cell, a held placeable brick that the brick registry knows, and a
  shape byte inside `BrickShape`. A hint-less event, or a hint that fails
  reach/occupancy/plausibility, resolves to `Action.None`. The resolver never
  throws for a missing hint.

## Authority apply

`ServerInteractionSystem` (`WaywardBeyond.Server/Systems/`) is the pump: on the
server tick between the replication Apply and Publish stages it builds the
interaction context and runs the pipeline (see
[interaction-pipeline](interaction-pipeline.md)).

The default handlers:

- `HintResolver` validates the hinted structure and cell against the
  authority store by identity and reach. It never raycasts, and it resolves
  with `InteractionResolver`.
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

### Edit audio

`ClientVoxelReconcileSystem.ApplyAuthoritativeEdit` plays the matching
break/place sound for **unpredicted** edits only: a remote player's mining and
construction is audible to everyone else. Edits correlating to the local
player's own pending predictions (confirm, snap, or revert) play nothing — the
prediction path already sounded (`PlayerInteractionService`). Break vs place is
decided by the resulting voxel (empty = break, filled = place); the material
class (rock vs metal) comes from the broken brick's tags, read from the cell
before the write (a break's result carries no material).
even when its registry ids differ from the server's. See
[brick-identity](brick-identity.md).

`ClientVoxelReconcileSystem` (`WaywardBeyond.Client/Systems/`, gated on
`Playing`) drains `ClientInbound.VoxelEdits` and correlates each echo against
the local `PendingInteractionComponent.Queue` by
`(entity, coordinate, sequence)`:

- voxel-matching echo → **confirm** (no-op);
- differing echo → **snap** to authority;
- prediction still pending past `REVERT_BOUND_SIM_TICKS` (40) with no echo →
  **revert** (the server rejected it).
- unpredicted edits (remote witnesses) apply directly.

A confirmed or reconciled edit marks the component dirty.
`VoxelEntityRebuildSystem` rebuilds the mesh and collider on the ECS thread. The
rebuild queues the previous renderers for disposal. See
[resource-lifetime](resource-lifetime.md).

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

- `WaywardBeyond.Gameplay/Interactions/` — resolver, request, handlers, registry
- `WaywardBeyond.Server/Systems/ServerInteractionSystem.cs`
- `WaywardBeyond.Server/Systems/VoxelColliderBuilder.cs`
- `WaywardBeyond.Client/Systems/*InteractionSystem.cs`
- `WaywardBeyond.Gameplay/Networking/{NetworkComponent,StageRing}.cs`
  (`StagedInteractions`)

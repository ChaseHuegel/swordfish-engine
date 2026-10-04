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
  `InputComponent` packet (`CodeGen/components.nsd`).
- **Client-authoritative active slot** rides `ClientOwned` `EquipmentComponent`.
- **Discrete button edges** are delivered as a `ClientOwned` `InteractionEvent`
  (uuid 15) — an extensible pseudo-union of nullable hint sub-messages. The
  server stages them into each player mirror's `InteractionStageBuffer`
  (`NetworkComponent.StagedInteractions`), keyed by `ServerTickAtSample`,
  newest-per-tick, deduped by `SequenceNumber`.

`InteractionEvent` carries `Entity`, `SequenceNumber`, `ServerTickAtSample`, and
`Kind` at the root. `Kind` is the button/edge and is not the union discriminator;
hint presence is. A hint-less event is valid and resolves to `Action.None`. See
[messages](networking-messages.md).

## Shared resolution

Targeting and outcome logic lives in shared code
(`WaywardBeyond.Shared.Gameplay/Interactions`), called identically by client
prediction and server authority:

- `SharedInteractionResolver` takes a ready world ray + optional `BrickInteraction`
  hint and produces `{ Action None | Break | Place, coordinate, voxel }`.
- Client targeting is a deterministic-from-ray routine
  (`TryGetBrickFromScreenSpace`: surface bias, reach-around probe rays, offset
  march-back). Server validation never raycasts; it validates the hinted
  structure + cell by identity.
- Rules: within reach; break requires an occupied cell (or reach-around) ; place
  requires the destination cell empty.
- A hint-less event, or a hint that fails reach/occupancy/plausibility
  (including one that disagrees with the ray-derived cell), resolves to
  `Action.None`. The resolver never throws for a missing hint.

## Authority apply

`ServerInteractionSystem` (`Server.Core/Systems/`, server tick between the
replication Apply and Publish stages):

- Builds an authority ray from the mirror transform + look.
- Resolves with `SharedInteractionResolver`.
- Applies the outcome on the structure's shared `VoxelObject`, rebuilds the
  structure `ColliderComponent` (`VoxelColliderBuilder`) so subsequent raycasts
  see the change, and re-derives `VoxelEntityDataComponent.Chunks`
  (+ `MarkDirty`) so saves track edits.
- Survival consumes a held item on place and grants the broken brick's loot via
  `IInteractionContent` against the server-owned `InventoryComponent`. Creative
  is free on both paths.

## Downstream replication

Every applied edit broadcasts to **all** clients as a `VoxelEditMessage` delta
(ordered/lossless by the transport):

```nsd
message VoxelEditMessage { ulong EntityUuid; int X, Y, Z; WaywardBeyond.Shared.Data.Voxel Voxel; uint Sequence; string? BrickId; }
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

`ClientVoxelReconcileSystem` (`Client.Core/Systems/`, gated on `Playing`)
correlates each echo against the local `PendingInteractionComponent.Queue` by
`(entity, coordinate, sequence)`:

- voxel-matching echo → **confirm** (no-op);
- differing echo → **snap** to authority;
- prediction still pending past `REVERT_BOUND_SIM_TICKS` (40) with no echo →
  **revert** (the server rejected it).
- unpredicted edits (remote witnesses) apply directly.

## Server mod API

Because every outcome resolves server-side, mods customize interactions
**server-side only**, no client mod.

- Mods register `IInteractionHandler`s into the shared, DI-singleton
  `IInteractionHandlerRegistry` (registered in `ServerComposition`), keyed on an
  `InteractionHandlerFilter` (`InteractionKind?`/`HeldItemID?`/`GameMode?`,
  null = match-any).
- After base validation, `ServerInteractionSystem` routes each resolution
  through the registry.
- A handler returning `InteractionResolution.None` **rejects** the interaction,
  returning the context's base resolution **allows** it, and returning a
  different resolution **overrides/augments** it.
- Matching handlers run in registration order; the last non-reject wins.

The interaction context components (`EquipmentComponent`, `InventoryComponent`,
`GameModeComponent`) are seeded from the client save via
`JoinRequest.CharacterSeed` at join and server-owned thereafter. See
[join](networking-join.md).

## Source of truth

- `WaywardBeyond.Shared.Gameplay/Interactions/` — resolver, handlers, registry
- `WaywardBeyond.Server.Core/Systems/ServerInteractionSystem.cs`
- `WaywardBeyond.Client.Core/Systems/*InteractionSystem.cs`
- `WaywardBeyond.Shared.Networking/Components/NetworkComponent.cs`
  (`StagedInteractions`)

## Tests that pin this

- `Swordfish.Tests/SharedInteractionResolverTests` — parity, break/place on
  occupied/empty, solid-wall march-back dead end, out-of-reach,
  hint-mismatch rejection.
- `Swordfish.Tests/ServerInteractionSystemTests` — survival break/place,
  creative free, hint-less/rejected no-op + consumed-once.
- Registry tests — reject, override, filter, registration-order override.
- `Swordfish.Tests` — `AppliedEditIsBroadcastToEveryClient`.
- `WaywardBeyond.Client.Core.Tests` — apply, `Playing` gate, confirm-as-no-op,
  snap-to-authority, expired-revert.
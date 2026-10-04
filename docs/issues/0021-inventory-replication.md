# Bug: Inventory moves don't replicate (foundation: inventory op messaging architecture)

- Type: bug
- Status: open
- Workflow: ../specs/issues.md

## Problem

Playtest: moving an item to a hotbar slot locally is not reflected on the
server, so placing a brick consumes and validates against the wrong slot.
Observed symptom: ice and lights placing as glass is a side effect of this
desync, not a brick-identity bug.

Root cause: `InventoryComponent` is registered ServerOwned (uuid 13), and
the replication `ApplyStage` rejects anything not ClientOwned
(`NetworkReplicationSystem.cs:164-167`), so the client's in-memory
inventory-UI edits never reach the server. `GetHeldItemID` and
`ConsumeHeldItem` (`ServerInteractionSystem.cs:215-280`) act on the
server's stale copy.

Locked model: the server is authoritative over inventory moves. The client
applies the move locally immediately as prediction; the server validates
it, applies it to its copy, and the authoritative `InventoryComponent`
echo (already a replicated ServerOwned component) flows downstream so an
invalid move is corrected automatically. This messaging architecture is
the foundation for future inventory-like systems: containers, equipment
slots, and crafting stations.

## Locked architecture

- New ClientOwned `InventoryEvent` envelope (registry uuid 16), union of
  nullable op members mirroring the `InteractionEvent` presence-based
  pattern; carries `SequenceNumber` for retransmit dedupe; bound to the
  sender's session per #0001.
- `SlotMoveOp { byte Mode, int FromSlot, int ToSlot, uint? Count }`:
  - `Mode`: `Exact = 0` (move `Count`, client-computed for partial drag and
    split), `AutoStack = 1` (server resolves destination stacks and the
    first empty slot - shift+click).
  - `Count`: nullable; **null = whole stack**, a value = exact count.
  - No explicit swap mode: the server resolves occupied-destination states
    deterministically (same item + capacity -> stack, otherwise swap).
- `SharedInventoryResolver` in `WaywardBeyond.Shared.Gameplay`
  (`SharedInteractionResolver` precedent) drives client prediction and
  server apply/validate. Validation rule:
  `0 <= slot < inventory.Contents.Length`.
- Staged per player mirror (precedent: `NetworkComponent.StagedInteractions`),
  sequence-deduplicated, consumed between the replication ApplyStage and
  interaction processing so a move and a place in the same tick resolve
  against the moved inventory.
- New spec doc `docs/specs/networking-inventory.md` containing the union,
  the op shapes, the resolution rules, the member reservation table
  (Sort, Drop, Transfer - the latter with slot-ref shapes for
  cross-inventory ops), and an "adding an op" checklist. No code is
  written for reserved members.

## Acceptance criteria

- [ ] Wire and registry per the locked architecture: `InventoryEvent` and
      `SlotMoveOp` added to `components.nsd`, uuid 16 registered
      ClientOwned, server stage/dedupe and session binding per #0001.
- [ ] `SharedInventoryResolver` implemented and used by both sides; the
      server validates per the rule, applies, and marks
      `InventoryComponent` dirty for the downstream echo.
- [ ] Client prediction applies moves locally; the authoritative echo
      corrects invalid or partial outcomes; a double-sent op applies
      exactly once.
- [ ] Same-tick ordering: a move and a place interaction in one tick
      resolve against the moved inventory.
- [ ] Tests: whole-stack (null count), partial, split, and AutoStack
      moves; out-of-bounds rejection with client correction; retransmit
      dedupe; glass-brick regression (placing consumes the item the
      server's copy holds).
- [ ] `docs/specs/networking-inventory.md` written (union, reservation
      table, add-an-op checklist, resolution rules); `networking-messages.md`
      and `networking-registry.md` updated (docs pass).
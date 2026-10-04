# Networking — Inventory Operations

One subject: how inventory moves replicate, from the client-op wire shape to
the authoritative server apply.

## Model

The server is authoritative over inventory moves. The client applies a move
locally immediately as prediction; the server validates it against its own
copy, applies it, and its authoritative `InventoryComponent` echo (already a
replicated ServerOwned component) flows downstream so an invalid move is
corrected automatically. The client reports a move as a discrete
`InventoryEvent`; the server never trusts it without applying the shared
resolver.

Wire and staging live in `CodeGen/components.nsd` and
`WaywardBeyond.Shared.Networking/Components/`. The resolver is
`WaywardBeyond.Shared.Gameplay/Interactions/SharedInventoryResolver.cs`.

## The envelope: `InventoryEvent`

```nsd
message InventoryEvent
{
    ulong Entity;            // player mirror address (bound to the sender session, see #0001)
    uint  SequenceNumber;    // retransmit dedupe
    SlotMoveOp? SlotMove;    // op payload (presence-based union, one member today)
}
```

`InventoryEvent` is registered ClientOwned under uuid 16. Staging mirrors
`InteractionEvent`: the inbound buffer lives on `NetworkComponent.StagedInventoryOps`,
the server consumes it between the replication ApplyStage and interaction
processing, and ops are sequence-deduplicated (a retransmitted sequence
overwrites its slot). Outbound, the client mirrors it with
`PendingInventoryComponent.Outbound`, drained by `ClientReplicationSystem` and
cleared only after a successful send (see
[replication](networking-replication.md)).

## The op: `SlotMoveOp`

```nsd
message SlotMoveOp
{
    byte  Mode;      // Exact = 0, AutoStack = 1
    int  FromSlot;
    int  ToSlot;     // Exact only; -1 for AutoStack
    uint? Count;     // null = whole stack, a value = exact count
}
```

- **Exact** moves `Count` (whole stack when null) from `FromSlot` to `ToSlot`.
  The client computes counts for partial drags and splits.
- **AutoStack** (shift+click) moves the whole source stack to the first
  same-item stack with capacity, else the first empty slot, scanning from the
  opposite region (a hotbar slot moves into the inventory, an inventory slot
  moves into the hotbar).
- There is no explicit swap mode: occupied-destination resolution is
  deterministic (same item + capacity → stack, otherwise swap; a partial move
  onto a different item is a no-op).
- Validation rule: `0 <= slot < inventory.Contents.Length`. An invalid op is a
  total no-op — it never throws and never partially applies.

## Resolution rules (shared resolver)

Both sides call `SharedInventoryResolver.Apply(ref inventory, in SlotMoveOp)`:

1. Bounds-check `FromSlot` (and `ToSlot` for Exact); a null or empty source
   stack (or `Count == 0`) is a no-op.
2. Exact: source removed by the count (clamped to the stack); destination
   empty → place; same item → stack up to capacity; different item → swap for
   a whole-stack move, no-op for a partial move.
3. AutoStack: scan the target region for the first same-item stack with
   capacity, then the first empty slot; no target → no-op.

The client computes Exact targets for split (first empty slot via
`FindFirstEmptySlot`) and partial drags; the server revalidates the same op
against its own copy, so drift is corrected by the echo.

## Member reservation table

Reserved op members follow the nullable-union pattern; no code is written for
them until the behavior is designed.

| Member | Reserved shape | Future behavior |
|---|---|---|
| `SlotMove` (done) | `SlotMoveOp?` | slot moves: Exact, AutoStack |
| `Sort` | `byte?` | sort the inventory (mode-qualified) |
| `Drop` | `UuidInt3?` | drop items into the world |
| `Transfer` | `TransferOp?` | cross-inventory moves (slot-ref shapes) |

## Adding an op (checklist)

1. Add the union member to `InventoryEvent` in `components.nsd` + codegen.
2. Add/choose the op message shape; reserved members above need a new message.
3. Define resolution rules in `SharedInventoryResolver` (client prediction AND
   server apply must call the same path).
4. Stage it: no new server machinery — the envelope already rides
   `NetworkComponent.StagedInventoryOps`.
5. Extend the client producer (inventory UI) to stage the op after predicting.
6. Test: resolver cases + a server e2e apply + echo, mirroring
   `Swordfish.Tests/SharedInventoryResolverTests.cs` and
   `ServerInventorySystemTests.cs`.

## Source of truth

- `WaywardBeyond.Shared.Networking/CodeGen/components.nsd`
- `WaywardBeyond.Shared.Networking/Components/{InventoryEvent,SlotMoveOp,InventoryOpStageBuffer,NetworkComponent}.cs`
- `WaywardBeyond.Shared.Gameplay/Interactions/SharedInventoryResolver.cs`
- `WaywardBeyond.Server.Core/Systems/ServerInventorySystem.cs`
- `WaywardBeyond.Client.Core/{Player/PlayerData.cs,Systems/ClientReplicationSystem.cs,UI/Layers/Inventory.cs}`

## Tests that pin this

- `Swordfish.Tests/SharedInventoryResolverTests.cs` — op resolution cases.
- `Swordfish.Tests/ServerInventorySystemTests.cs` — staged apply, dedupe, echo.
- `WaywardBeyond.Client.Core.Tests/ClientReplicationInteractionTests.cs` —
  outbound op drain and clear-on-success.
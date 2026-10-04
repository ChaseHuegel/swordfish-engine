# Bug: Server accepts client-addressed snapshots for any entity (cross-client command injection)

- Type: bug
- Status: done
- Workflow: ../specs/issues.md

## Problem

`NetworkReplicationSystem.ApplyStage` ignores the `clientId` the hub tags
each inbound frame with and addresses staged input/interaction commands
purely by the client-supplied `snapshot.Entity` uuid
(`NetworkReplicationSystem.cs:80-87`, `ApplyComponent` at `:155-217`). A
client can forge snapshots referencing another player's mirror uuid and
inject `InputComponent`/`InteractionEvent` payloads into that player's
staged buffers. Consequences:

- Injected `InputComponent` drives the victim's mirror through the shared
  simulation step, moving another player.
- Injected `InteractionEvent` resolves on the server as the victim acting:
  breaking/placing as them, consuming the victim's held items, and granting
  loot to the victim's inventory (`ServerInteractionSystem.ConsumeHeldItem`
  and `GrantLoot` operate on the target player's components).
- An unknown uuid also materializes a brand-new server entity
  (`store.Alloc(entityUuid)`), an unauthenticated entity-creation primitive.

A connection must only ever write to its own session entity.

## Acceptance criteria

- [ ] `ApplyStage` binds every inbound `ClientOwned` snapshot to the
      sender's session entity; a wire uuid resolving to any other entity is
      ignored and never allocated, staged, or applied server-side.
- [ ] Test: client B's snapshot addressed at client A's entity uuid does
      not stage input or interactions on A's mirror, and creates no entity.
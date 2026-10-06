# Bug: Rejoining a different save tears down the shared world

- Type: bug
- Status: in-progress
- Workflow: ../specs/issues.md

## Problem

A connected client that returns to the menu and joins a different save keeps
its connection bound to the old world. World routing drains only
`PendingJoins` (`ServerWorldHost.cs:145`). `ServerJoinSystem.HandleLeave` ends
the session but leaves the connection on the world hub
(`ServerJoinSystem.cs:120`). The next `JoinRequest` is therefore consumed by
the old world's `ServerJoinSystem` (`ServerJoinSystem.cs:74`), which calls
`WorldSaveService.LoadLevel` (`ServerJoinSystem.cs:164`).

`LoadLevel` calls `Unload` when the level changes
(`WorldSaveService.cs:232`). `Unload` frees every `NetworkComponent` entity -
all structures and every player's mirror - with no despawn and no session
teardown (`WorldSaveService.cs:299`, `:572`). `_sessions` then maps a client to
a freed entity, so `NetworkReplicationSystem` sees a null session uuid and
logs `Ignoring client-owned snapshot ... only the sender's session entity 0 is
writable` per component per tick (`NetworkReplicationSystem.cs:258`).

Playtest evidence (one server process):

- `Loaded world` logged once while two levels (`c7b45...`, `5ea5b5ff...`) were
  loaded into that world's store.
- Session ids run `0..6` across both "worlds".
- 65,683 `only the sender's session entity ... is writable` warnings, 32,887
  with session uuid `0`; 107 `without a session` warnings.

## Acceptance criteria

- [ ] A client that leaves a world and joins a different save lands in that
      level's own world; the original world and its players are untouched.
- [ ] `ServerJoinSystem.HandleJoin` refuses a level mismatch with an error log.
      It never switches a live world's active level.
- [ ] A connection returns to `PendingJoins` after `LeaveGame`, so the next
      `JoinRequest` is routed by `ServerWorldHost`.
- [ ] A regression test covers a cross-level rejoin while another session is
      live, and asserts no cross-world despawns and no session-uuid warnings.

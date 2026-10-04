# Bug: Disconnect handling has holes outside Playing — save screen never returns to menu, join hangs after disconnect

- Type: bug
- Status: done
- Workflow: ../specs/issues.md

## Problem

Playtest findings: disconnecting while on the save screen never returns the
client to the main menu, and after a disconnect the player can still
attempt to join and hangs in Loading indefinitely. The current handling
has three gaps:

- `ClientDisconnectSystem` (`ClientDisconnectSystem.cs:19-59`) relies
  solely on `TransportManager.RemoteDisconnected`, which is raised only
  while a remote transport is attached and subscribed. Returning to the
  save screen runs `TransportManager` teardown paths that can detach or
  dispose the remote (`TransportManager.cs:78-88`), so a server death at
  that point goes unnoticed and no menu transition happens.
- In-flight menu operations never complete when the server dies
  mid-request: `WorldsClient` (`WorldsClient.cs:39-74`) completes its
  `TaskCompletionSource`s only when a response arrives, with no timeout
  and no fault path - a save/world op against a vanished server awaits
  forever.
- A join issued with no active connection (`TransportManager.Send` fails
  with "No active connection") is never surfaced: `ClientJoinSystem` keeps
  its `_sent` state and sits in Loading (`ClientJoinSystem.cs:67-97`);
  only the #4 join timeout (not yet landed) would bound it.

## Acceptance criteria

- [x] A remote disconnect while in any menu state (save screen, world
      list, any page) returns the client to the main menu with the
      connection-lost notice, regardless of when the transport was
      attached.
- [x] `WorldsClient` pending operations are faulted (or otherwise
      completed) when the connection drops, so the save screen and
      world-management UI never wait forever on a dead server.
- [x] A join attempt with no active connection fails fast with a
      user-visible notice instead of entering Loading.
- [x] Tests: disconnect during the save screen returns to the menu; a
      `SaveWorldRequest` in flight during a disconnect completes with
      failure; a join with no active transport reports failure without
      entering Loading.
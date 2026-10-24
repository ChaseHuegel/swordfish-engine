# Improvement: Restore the loopback session after a remote disconnect

- Type: improvement
- Status: done
- Workflow: ../specs/issues.md

## Problem

`TransportManager` binds the in-process loopback at construction. It swaps the
active connection to a remote transport on `ConnectRemote`. `Disconnect` sets
the active connection to null. No path binds the loopback again.

After a remote disconnect, `ClientDisconnectSystem` saves the character and
returns to the menu with no active connection. A local save load then sends
into a null transport and fails. The menu cannot list local saves. The player
must restart the application.

Keeping the loopback connected while the player is remote is not a fix. Its
server peer stays accepted and receives heartbeats. The client inbound queues
accumulate frames that no system drains.

## Acceptance criteria

- [x] `ClientRpcHost` connects to the loopback at startup and marks it active.
- [x] A successful `ConnectRemote` closes the loopback `RpcClient` and its
      connection before it switches to the remote.
- [x] A remote disconnect closes the remote and creates a fresh loopback
      `RpcClient` and connection. The loopback becomes active.
- [x] `Disconnect` restores the loopback instead of leaving no active
      connection.
- [x] The loopback server transport accepts a new client connection after the
      previous one closes. Repeated remote and local switches do not leak a
      peer.
- [x] `ClientInbound` is cleared on every switch. No stale heartbeat,
      snapshot, or level chunk reaches a system.
- [x] `RemoteDisconnected` still fires for a remote drop. The connection-lost
      toast and teardown are unchanged.
- [x] `GameClient.IsLocal` and `GameClient.IsConnected` report the restored
      loopback after a remote drop.
- [x] The menu lists local saves after the restore. A local join succeeds
      without an application restart.
- [x] `ProfileSettings.LastServerMode` is unchanged by the restore. Continue
      keeps its current behavior.
- [x] A test drops a remote connection and then lists levels and joins a local
      world. A second test switches remote, local, and remote again.
- [x] The docs pass updates `networking-transports`,
      `networking-overview`, and `networking-join`.

## Dependencies

- 0048 adds `ClientRpcHost` and the inbound queues.
- 0046 makes the loopback transport accept sequential client connections.

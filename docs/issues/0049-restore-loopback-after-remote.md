# Improvement: Restore the loopback session after a remote disconnect

- Type: improvement
- Status: open
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

- [ ] `ClientRpcHost` connects to the loopback at startup and marks it active.
- [ ] A successful `ConnectRemote` closes the loopback `RpcClient` and its
      connection before it switches to the remote.
- [ ] A remote disconnect closes the remote and creates a fresh loopback
      `RpcClient` and connection. The loopback becomes active.
- [ ] `Disconnect` restores the loopback instead of leaving no active
      connection.
- [ ] The loopback server transport accepts a new client connection after the
      previous one closes. Repeated remote and local switches do not leak a
      peer.
- [ ] `ClientInbound` is cleared on every switch. No stale heartbeat,
      snapshot, or level chunk reaches a system.
- [ ] `RemoteDisconnected` still fires for a remote drop. The connection-lost
      toast and teardown are unchanged.
- [ ] `GameClient.IsLocal` and `GameClient.IsConnected` report the restored
      loopback after a remote drop.
- [ ] The menu lists local saves after the restore. A local join succeeds
      without an application restart.
- [ ] `ProfileSettings.LastServerMode` is unchanged by the restore. Continue
      keeps its current behavior.
- [ ] A test drops a remote connection and then lists levels and joins a local
      world. A second test switches remote, local, and remote again.
- [ ] The docs pass updates `networking-transports`,
      `networking-overview`, and `networking-join`.

## Dependencies

- 0048 adds `ClientRpcHost` and the inbound queues.
- 0046 makes the loopback transport accept sequential client connections.

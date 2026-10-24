# Bug: Transport lifecycle leaks — dead peers never disposed, host client registry never pruned

- Type: bug
- Status: done
- Workflow: ../specs/issues.md

## Problem

On a peer disconnect the socket and its owning objects are never cleaned
up:

- `TcpTransport.MarkBroken` (`TcpTransport.cs:378-403`) cancels the
  send/keepalive loops and raises `OnDisconnected` but never closes the
  socket or stream - the peer transport lingers (open handle, queues)
  until `Disconnect`/`Dispose` is called by someone.
- `LanHost.RemoveClient` (`LanHost.cs:162-168`) removes the peer from the
  hub but never disposes the `TcpTransport`, so a host that has served N
  clients over many sessions accumulates N undrained transports.
- `TcpServerHost._clients` (`TcpServerHost.cs:30,80,92-104`) is keyed by
  transport and never pruned on disconnect - every accepted client stays
  in the dictionary until host `Dispose`, so shutdown also re-disposes
  stale transports without bound.
- `IsConnected` (`TcpTransport.cs:47`) returns `TcpClient.Connected`,
  which reflects the last I/O result and stays `true` on a fully dead
  peer - misleading connection state surfaced through
  `TransportManager.IsConnected`.

Decisions (locked): the transport never disposes itself - indirect and
unclear for an object to own its own cleanup. The owner listening for
disconnects is responsible for disposal on disconnect
(`LanHost.RemoveClient` and the client's `TransportManager` path).
`IsConnected` must reflect the true running and connected state.

## Acceptance criteria

- [x] The disconnect path deterministically closes the socket and
      disposes the transport exactly once, performed by its owner
      (server: `LanHost` on `OnDisconnected`; client: `TransportManager`
      path) - never by the transport itself, and never twice.
- [x] `TcpServerHost` prunes `_clients` when a peer disconnects, so the
      registry holds only live peers (subscribing to the same
      `OnDisconnected` signal as the owner).
- [x] `IsConnected` reflects the true state: false once the receive or
      send loop has stopped (peer gone or intentional disconnect), not the
      stale `TcpClient.Connected` result.
- [x] Test: repeated connect/disconnect cycles against `TcpServerHost` +
      `LanHost` leave no growth in `_clients`, `_clientIds`, or hub
      `Count`, and no live socket handles for dead peers.
# Improvement: Add the RPC migration test suite and networking docs pass

- Type: improvement
- Status: in-progress
- Workflow: ../specs/issues.md

## Problem

The RPC migration changes every wire path, the join flow, the snapshot path,
and the connection lifecycle. The existing tests cover the legacy transport
and the old join flow. The networking specs still describe per-type demux,
the drop-oldest queue, the 16 MB frame cap, FIFO level responses, and the
2N+1 thread model.

The migration is not complete until the new behavior is pinned and the docs
match the code.

## Acceptance criteria

- [x] Fragment tests cover a zero-length payload, an exact boundary, one byte
      over the boundary, a stale incomplete batch, and a complete batch that
      arrives after a newer batch.
- [x] Loopback RPC tests cover a call, a notification, a stream, a timeout,
      a cancellation, and a disconnect. They replace
      `Swordfish.Tests/LocalConnectionTests.cs`.
- [x] A real TCP test binds an ephemeral port and covers connect, join, stream,
      play, save, and disconnect.
- [ ] A join test proves that all level entities and
      `LevelStreamComplete` arrive in order, and that a partial stream does
      not enter `Playing`.
- [x] A snapshot test proves that a full sync over the fragment budget
      reassembles and applies atomically. A congestion test proves that no
      removal, one-shot component, interaction edge, or inventory op is lost.
- [x] A stalled-peer test proves that the backlog policy closes the peer with
      `BacklogLimit` instead of dropping frames.
- [x] An idle-peer test proves that `IdleTimeout` closes a silent peer and
      keeps a peer that sends a heartbeat.
- [x] A disconnect-reason test proves that the client records the reason and
      that the server transport wrapper records it by `ClientId` for EOF,
      idle timeout, backlog, and local close.
- [x] A reconnect test proves that a remote drop restores the loopback, lists
      local levels, and joins a local world.
- [x] A malformed payload test proves that a request answers `InvalidParams`,
      a notification is dropped, a chunk fails the call with `InvalidRequest`,
      and the connection stays open.
- [x] A permission test proves that a denied menu call returns the app error
      code and does not create or delete data.
- [x] A registry test proves that method ids are unique and directions match.
- [x] The stats overlay test proves that packet and byte totals match the
      peer's opposite counters. A batching test proves that `Writes` falls
      below `PacketsSent` when batching is enabled.
- [x] `docs/specs/networking-rpc.md` is complete. It covers the service
      catalogue, method ids, error codes, send policy, backlog policy, idle
      timeout, fragment rule, loopback behavior, and threading model.
- [x] `networking-overview`, `networking-messages`, `networking-transports`,
      `networking-join`, `networking-replication`, `networking-worlds`,
      `networking-inventory`, `networking-voxel-edits`,
      `networking-prediction`, `config-schemas`, `composition`, `chat`, and
      `permissions` match the code and link to `networking-rpc`.
- [x] The docs remove the stale claims about per-type demux, the drop-oldest
      snapshot queue, 16 MB frames, FIFO level responses, `ConnectionClaims`,
      and the 2N+1 thread model.
- [x] `docs/README.md` indexes `networking-rpc` and the upstream transport
      proposal.
- [x] `dotnet build`, `dotnet test Swordfish.Tests`, `dotnet test Reef.Tests`,
      and `dotnet test Reef.Text.Tests` pass.

## Dependencies

- 0046 through 0050.
- nsd-rpc 0.3.0 supplies the backlog policy, idle timeout, metrics, and
  batching behavior that the tests pin.

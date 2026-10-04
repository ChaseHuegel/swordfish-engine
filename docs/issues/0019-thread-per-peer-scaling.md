# Research: Thread-per-peer transport scaling (3N+1 network threads per host)

- Type: research
- Status: open
- Workflow: ../specs/issues.md

## Problem

Each `TcpTransport` peer runs three dedicated background threads (receive,
send, keepalive) plus the host's accept thread - 3N+1 network threads for
N connected remote clients (plus the server world thread). This is the
simplest correct design for blocking socket I/O and entirely fine for LAN
co-op scale (N <= 8), but `networking-transports.md` names a "dedicated"
server as a goal, and 3N+1 threads per host grows without bound: 32
players -> ~100 threads, 64 -> ~200, with per-peer queue memory on top.
Before any architecture moves, the cost profile and a target player count
should be established: context-switch cost, per-peer memory (queues,
buffers), and the actual bottleneck (CPU serialization, socket throughput,
or memory).

## Scope to research and answer

- Establish a target client count for the host/dedicated-server goal.
- Measure per-peer cost (committed threads, queue memory, CPU) at
  N = 2/8/16/32 using the existing transports over loopback sockets
  (extend the `TcpTransportTests`/`TcpServerHostTests` harness).
- Identify whether the bottleneck is thread count, per-peer queue memory,
  or serialization CPU.
- Evaluate replacement designs against the current one: a single async
  receive loop (e.g. `SocketAsyncEventArgs`/`ValueTask` I/O)
  multiplexing all peers with per-peer queue state, vs. keeping per-peer
  threads and documenting hard limits. Include the interplay with the #3
  and #4 queues and the `MaxReceiveWindow` fairness from #6.
- Recommend a path with a decision: migrate to async multiplexed I/O, or
  accept and document the thread model with a proposed cap.

## Acceptance criteria

- [ ] A written proposal (proposed home: `networking-transports.md`
      revision or a dedicated doc) with measured numbers at the target
      client counts, a clear bottleneck analysis, and a single
      recommended direction.
- [ ] The recommendation is approved by the user before any
      implementation begins.
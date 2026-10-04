# Bug: Interaction edges dropped on TCP congestion — unconditional outbound clear

- Type: bug
- Status: done
- Workflow: ../specs/issues.md

## Problem

`ClientReplicationSystem.DrainInteractions`
(`ClientReplicationSystem.cs:89-116`) snapshots the player's outbound
`InteractionStageBuffer`, emits one `ComponentSnapshot` per edge, then
unconditionally clears the buffer (`pending.Outbound.Clear()`, `:115`)
regardless of whether the send succeeded. The in-code comment claims
"TCP/relayed clients keep their own outbound framing" - false; this is the
same code path for every transport. When the transport cannot deliver
(failed `Send`, or the drop policy fixed in #3), staged edges are
discarded and the docs' "lossless; only latency trades" promise breaks - a
mine-mode click or place is lost silently.

## Acceptance criteria

- [x] The outbound buffer is cleared only after the containing snapshot is
      successfully sent; on failure the edges remain staged and are
      re-emitted on a later tick.
- [x] Test: with a transport whose `Send` fails (or a full per-tick
      queue), staged edges survive and are delivered on the next
      successful tick, exactly once.
- [x] Stale comment removed; the transport-dependence of edge delivery is
      documented in `networking-replication.md`.
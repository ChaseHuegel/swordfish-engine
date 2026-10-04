# Improvement: Network logging — disconnect reasons and config-gated message tracing

- Type: improvement
- Status: done
- Workflow: ../specs/issues.md

## Problem

A disconnect tells you nothing: `TcpTransport.OnDisconnected` is a bare
no-arg `Action` (`TcpTransport.cs:55`), and every detection site
(`ReceiveLoop` EOF/exception, `SendLoop` write failure, timeouts,
`MarkBroken`) collapses into the same silent break. Debugging a dropped
session means guessing EOF vs timeout vs write failure. There is also no
per-message tracing: the only transport-level signal is the
unknown-type-tag warning (`TcpTransport.cs:353`); deserialization failures
surface only as `Result` failures that callers discard. The #0004
backlog-disconnect policy will add a fifth reason that must be visible.

Locked decision: the trace-logging flag is a WaywardBeyond config key,
`NetworkingSettings.TraceLogging` (bool, `network.toml`) - not an engine
`DebugSettings` value.

## Acceptance criteria

- [x] Disconnects carry a reason: `OnDisconnected` gains a reason (e.g. an
      enum: peer-closed-EOF, read-error, read/write-timeout, write-error,
      backlog-limit) reported by each detection site, logged at the
      transport and by consumers (`LanHost`,
      `TransportManager`/`ClientDisconnectSystem`).
- [x] Per-message trace logging for sent and received frames (type, byte
      count, direction) gated by `NetworkingSettings.TraceLogging`
      (default false), so production logs stay clean. Config key
      documented in the `networking-transports.md` table.
- [x] Frames dropped for unknown type tags and decode failures are logged
      at warn with the discriminating detail (type name, byte count).
- [x] Tests: each disconnect scenario reports the correct reason (EOF,
      timeout, write failure; backlog-limit once #0004 lands); trace logs
      can be enabled without affecting message flow (docs pass:
      `networking-transports.md`).
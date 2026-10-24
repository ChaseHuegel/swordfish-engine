# Improvement: NetworkRegistry registration failures are silent or deferred

- Type: improvement
- Status: done
- Workflow: ../specs/issues.md

## Problem

Registry misconfiguration surfaces late or not at all:

- `NetworkRegistry.Register` returns `false` silently on duplicate
  type/uuid or `Uuid.Null` (`NetworkRegistry.cs:66-83`); no caller checks
  the result, so a colliding or mistyped registration is invisible at
  startup.
- `NsdComponentCodec<T>` validates its nsd `Serialize`/`Deserialize` in
  the **static constructor** (`NsdComponentCodec.cs:19-31`), which only
  runs on first use - a component registered without generated nsd
  methods fails mid-game on the wire, deep inside the publish stage, not
  at registration.
- `NsdComponentCodec.Serialize` returns `[]` when the component is not on
  the entity (`:35-43`), and the delta paths skip `payload.Length == 0`
  snapshots (`NetworkReplicationSystem.cs:237-240`,
  `ClientReplicationSystem.cs:80-84`) - conflating "component absent"
  with "valid empty payload" and masking precisely the bugs above.
- The registry is a process-wide static shared by both worlds; a
  registration conflict between client and server wiring reports nothing.

Decisions (locked):

- `Register` returns the `Result` type with a contextual message naming
  the failure (duplicate type, duplicate uuid, `Uuid.Null`, invalid
  codec); callers log errors when a failed result is received.
- Fail-fast policy: invalid registrations and invalid types that are part
  of the game and engine wiring are fatal at startup (exception, loud and
  early - caught in development). Content that can come externally from
  mods fails registration without crashing the game: the error is logged
  and the game continues.
- Codec validity is proven at registration time, not on first use.

## Acceptance criteria

- [x] `Register` returns a `Result` carrying a contextual failure message
      (duplicate type, duplicate uuid, `Uuid.Null`, invalid codec); no
      call path ignores a failed result - built-in wiring treats failure
      as fatal at startup, mod-facing paths log the error and continue.
- [x] Codec validity is proven at registration (expected: static ctor
      forced via `RuntimeHelpers.RunClassConstructor` or equivalent), so
      a component without generated nsd methods fails at registration.
- [x] The delta paths treat a missing component during serialize as
      distinct from an empty payload (log and skip explicitly); the
      `[]`-payload masking is removed or made unambiguous.
- [x] Tests: duplicate registration returns a failed `Result` with the
      reason; a non-nsd component registration fails at registration
      time; mod-style registration failure logs and continues.
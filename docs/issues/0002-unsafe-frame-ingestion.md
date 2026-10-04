# Bug: Unsafe frame ingestion — unbounded lengths, out-of-bounds deserialization (nsdc codegen), client-caused tick aborts

- Type: bug
- Status: done
- Workflow: ../specs/issues.md

## Problem

Three defects in the frame-ingestion path:

1. **No frame-size cap.** `TcpTransport.ReceiveLoop` trusts the 4-byte
   length prefix with no maximum (`TcpTransport.cs:324-330`). A peer can
   send `0x7FFFFFFF` and force an instant ~2 GB allocation (OOM), or
   repeated 100 MB frames as a cheap DoS.

2. **nsd-generated deserializers perform out-of-bounds reads.** The
   defect is structural in every emitted `Unpack` in every
   `CodeGen/Output/*.cs`; fixing it robustly requires an upstream nsdc
   change, so the exact locations follow for submission.
   - **B1 — unguarded fixed-size reads.** The loop guard
     `while (readsCompleted < N && offset + 2 < end)` only guarantees two
     remaining bytes, but the field reads that follow take 4-8
     (`*((uint*)offset)` at `network.cs:1117`, `*((ulong*)offset)` at
     `:1196`, floats at `components.cs:412`). Reads cross `end` by up to
     6 bytes - off the array end into adjacent heap objects.
   - **B2 — forgeable nested bounds.** Nested messages are decoded with a
     caller-supplied length
     (`Components[i].Unpack(buffer, g__Components_Start, g__Components_ObjectLength)`,
     `network.cs:1165`); the inner `Unpack` sets `end = b + length`
     (`network.cs:1093`) from that forged value, so the inner guard
     validates against an attacker-computed end. A crafted frame can walk
     arbitrary adjacent heap memory (up to 65535 bytes per element, up to
     65535 elements) and pre-allocate `new ComponentSnapshot[65535]`
     (`:1158`) from a near-empty frame.
   - **B3 — silent ushort truncation on serialize.** Collection and
     payload lengths serialize as `ushort` (`network.cs:119`, `:656`,
     `:675`); the `GetSize()` walk is untruncated, so written headers
     disagree with the body for anything over 65535 bytes/elements and the
     reader misparses trailing bytes as new field ids. Any array, string,
     or byte array over 64 KB is unrepresentable on the wire today.

   Reads return garbage - which is then applied to the store (silent
   desync) - or fault across a page boundary (AccessViolation, process
   crash). Our side cannot bound these reads without reimplementing the
   schema walk (a second parser); the guard-page trick fails because
   element lengths are attacker-controlled and the inner `end` is forged.

   Suggested nsdc fix: two invariants in emitted `Unpack` - `end` is
   always the real buffer end, never derived from wire data; and before
   every read, allocation, or copy, verify `offset + readSize <= end`,
   validating nested lengths against `end - offset` before recursing.
   Loop shape: break (treat as truncated) when a field's size check
   fails. For B3, widen length headers to `uint` (or varint), or abort
   rather than truncate when a value exceeds `ushort.MaxValue`.

3. **Client-caused exceptions abort the whole tick.**
   `ApplyComponent` runs uninstrumented inside the server tick
   (`NetworkReplicationSystem.cs:155-217`): `store.Alloc(entityUuid)`
   throws `InvalidOperationException` on a client-chosen duplicate uuid
   (`DataStore.cs:61-63`) and codec `Apply` can throw on malformed
   payloads. The blanket `catch` in `ServerContext.Update`
   (`ServerContext.cs:139-142`) then aborts the whole tick - physics step
   and replication publish skipped - so one hostile client can freeze
   simulation for every client.

## Acceptance criteria

- [x] `NetworkingSettings.MaxFrameBytes` (new config key, `network.toml`,
      default 16 MiB) caps the frame length prefix; `TcpTransport` can
      never allocate a frame buffer above it. Config table updated in
      `networking-transports.md`.
- [x] `ApplyStage` isolates per-client failures: a throw or failed decode
      from one client is logged and skipped; it cannot abort the world
      tick, and duplicate-uuid snapshots never throw out of the stage.
- [x] nsdc bug prepared with the B1-B3 locations and the suggested guard
      shape; submission to the upstream tracker is tracked as
      [issue 0037](0037-nsdc-unpack-safety.md). Once the fixed codegen is
      adopted, a malformed-frame regression test is added there.
- [x] Tests: oversized prefixes and truncated frames are rejected without
      allocation; one client's malformed frames do not affect other
      clients or the world step.
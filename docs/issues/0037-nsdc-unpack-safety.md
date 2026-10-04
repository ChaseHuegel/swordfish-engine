# Improvement: nsdc unpack safety — submit the B1-B3 deserializer defect upstream

- Type: improvement
- Status: open
- Workflow: ../specs/issues.md

## Problem

Every nsdc-generated `Unpack` bounds its reads against attacker-derived
lengths, so a crafted frame can read or walk out of bounds. Frame-level
mitigations now cap the wire (`NetworkingSettings.MaxFrameBytes`) and isolate
per-client decode failures, but the structural defect lives in the generated
code. Fixing it robustly requires an upstream nsdc change. The exact locations
follow for submission.

### B1 — unguarded fixed-size reads

The loop guard `while (readsCompleted < N && offset + 2 < end)` only guarantees
two remaining bytes, but the field reads that follow take 4-8. Reads cross
`end` by up to 6 bytes, off the array end into adjacent heap objects.
Locations (generated code under `WaywardBeyond.Shared.Networking/CodeGen/Output/`):

- `network.cs:1117` — `*((uint*)offset)`
- `network.cs:1196` — `*((ulong*)offset)`
- `components.cs:412` — float reads.

### B2 — forgeable nested bounds

Nested messages decode with a caller-supplied length
(`Components[i].Unpack(buffer, g__Components_Start, g__Components_ObjectLength)`,
`network.cs:1165`); the inner `Unpack` sets `end = b + length`
(`network.cs:1093`) from that forged value, so the inner guard validates
against an attacker-computed end. A crafted frame can walk arbitrary adjacent
heap memory (up to 65535 bytes per element, up to 65535 elements) and
pre-allocate `new ComponentSnapshot[65535]` (`network.cs:1158`) from a
near-empty frame.

### B3 — silent ushort truncation on serialize

Collection and payload lengths serialize as `ushort` (`network.cs:119`,
`:656`, `:675`); the `GetSize()` walk is untruncated, so written headers
disagree with the body for anything over 65535 bytes/elements and the reader
misparses trailing bytes as new field ids. Any array, string, or byte array
over 64 KB is unrepresentable on the wire today.

## Suggested nsdc fix (for the submission)

Two invariants in emitted `Unpack`:

1. `end` is always the real buffer end, never derived from wire data.
2. Before every read, allocation, or copy, verify `offset + readSize <= end`,
   validating nested lengths against `end - offset` before recursing.

Loop shape: break (treat as truncated) when a field's size check fails.

For B3, widen length headers to `uint` (or varint), or abort rather than
truncate when a value exceeds `ushort.MaxValue`.

## Acceptance criteria

- [ ] The defect report (the locations and the suggested guard shape above)
      is submitted to the nsdc issue tracker.
- [ ] Once the fixed codegen is adopted in this repo, a malformed-frame
      regression test is added under `Swordfish.Tests` proving a crafted
      frame cannot read out of bounds or over-allocate.
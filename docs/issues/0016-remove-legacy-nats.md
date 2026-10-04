# Improvement: Remove the legacy packet-relay networking path (NATS persistence stays)

- Type: improvement
- Status: open
- Workflow: ../specs/issues.md

## Problem

Two networking stacks coexist. The pre-branch NATS packet-relay design is
still in the tree, unused by any registration:

- `WaywardBeyond.Server.Core/Streaming/PacketStreamClient.cs` (blocks on
  `tcs.Task.Result` of a `Task.Run`, `:61-63`)
- `WaywardBeyond.Server.Core/Serialization/PacketNatsSerializer.cs`
- `WaywardBeyond.Server.Core/Networking/IProtocol.cs` and
  `ProtocolV1.cs` - `ProtocolV1.Send` is a silent no-op returning success
  (`ProtocolV1.cs:8-11`)
- The legacy `WaywardBeyond.Server.Core/CodeGen/` packet schemas that feed
  the `Torches.Networking.Models` namespace

Nothing in the new architecture registers or depends on these (verified:
no DI references). Scope correction (locked): this removal covers only the
dead **packet-relay** remnants. `PersistentNatsProcess` and the
NATS-backed `KeyValueStore` are load-bearing - the client starts the
embedded NATS server (`Entry.cs:74`), and the server resolves the
persistence store through it (`ServerComposition.cs:14-20`) - and remain
in place. See #0022 (NATS child lifecycle) and #0036 (persistence backend
re-evaluation).

## Acceptance criteria

- [ ] `PacketStreamClient`, `PacketNatsSerializer`, `IProtocol`,
      `ProtocolV1`, and the legacy `Server.Core/CodeGen` packet schemas
      (and their generated output) are removed; `dotnet build` is clean
      and no reference to `Torches.*` or `IProtocol` remains.
- [ ] `PersistentNatsProcess` and the NATS-backed `KeyValueStore`
      persistence path are untouched.
- [ ] `networking-overview.md`'s source-of-truth list no longer implies a
      second packet protocol (docs pass); the NATS persistence role stays
      documented per `persistence.md`.
- [ ] No behavioral change: the TCP/nsd path and NATS persistence are
      untouched (existing networking and persistence tests green).
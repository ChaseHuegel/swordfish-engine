# Improvement: Remove the legacy NATS/Torches networking path

- Type: improvement
- Status: open
- Workflow: ../specs/issues.md

## Problem

Two networking stacks coexist. The pre-branch NATS design is still in the
tree, unused by any registration:

- `WaywardBeyond.Server.Core/Streaming/PacketStreamClient.cs` (blocks on
  `tcs.Task.Result` of a `Task.Run`, `:61-63`) and
  `PersistentNatsProcess.cs`
- `WaywardBeyond.Server.Core/Serialization/PacketNatsSerializer.cs`
- `WaywardBeyond.Server.Core/Networking/IProtocol.cs` and
  `ProtocolV1.cs` - `ProtocolV1.Send` is a silent no-op returning success
  (`ProtocolV1.cs:8-11`)
- The legacy `WaywardBeyond.Server.Core/CodeGen/` schemas
  (`Packet`/`Entity`/`World`/`Auth`/`Chat`) that feed the
  `Torches.Networking.Models` namespace

Nothing in the new architecture registers or depends on these (verified:
no DI references). They confuse the codebase - a class named
`ProtocolV1` that does nothing sits beside the real TCP protocol.

## Acceptance criteria

- [ ] The five files and the legacy `Server.Core/CodeGen` schemas (and
      their generated output) are removed; `dotnet build` is clean and no
      reference to `Torches.*` or `IProtocol` remains.
- [ ] `networking-overview.md`'s source-of-truth list no longer implies a
      second protocol path (docs pass).
- [ ] No behavioral change: the TCP/nsd path is untouched (existing
      networking tests green).
# Bug: nsd-rpc keeps a canceled inbound entry with a disposed token source

- Type: bug
- Status: done
- Workflow: ../specs/issues.md

## Problem

`RpcRegistry` dispatch creates a `CancellationTokenSource` per inbound request
and attaches it to the inbound entry (`NsdRpc/Handlers/RpcDispatcher.cs`). The
dispatcher disposes the source in `finally`.

A cancellation or a call deadline marks the inbound entry `Cancelled`.
`RpcPeer.ClaimInbound` returns `Cancelled` without removing the entry from
`_inbound`. The entry therefore keeps the disposed source.

`RpcPeer.FaultPending` runs on every disconnect and calls `Cancel()` on every
inbound source. A canceled entry therefore throws `ObjectDisposedException`
from the disconnect path. The exception escapes the transport's
`Disconnected` event: on the loopback it reaches the caller of `Close`, and on
TCP it reaches the receive loop.

The game reproduces this when a join or stream call is canceled and the
connection closes afterwards.

## Acceptance criteria

- [x] A canceled request leaves no entry in the inbound table.
- [x] A response or failure after cancellation removes the entry.
- [x] `FaultPending` tolerates a token source that is already disposed.
- [x] A test cancels a call, waits for the handler to return, then disconnects.
      The disconnect raises no exception.
- [x] The fix ships in a package version the game can pin.

## Dependencies

- Upstream repository: `ChaseHuegel/nsd-rpc`.
- Found while working 0047 and 0048.

## Resolution

Fixed upstream in commit `ba882611` and released as nsd-rpc 0.3.1.
`ClaimInbound` removes the entry, and `FaultPending` tolerates a disposed token
source.

The repository now pins 0.3.1. `LoopbackRpcTests` closes the connection after a
timeout and after a cancellation. The cancellation test waits for the handler to
return before it disconnects.

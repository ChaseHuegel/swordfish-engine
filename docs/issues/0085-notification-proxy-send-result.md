# Improvement: nsd-rpc notification proxies swallow the send result

- Type: improvement
- Status: open
- Workflow: ../specs/issues.md

## Problem

`RpcPeer.Notify` returns false when the reliable send queue rejects a frame.
`GameServerApiClient` and `GameClientApiClient` notification proxies discard that
value, so a caller that uses the typed proxy cannot tell a delivered message from
a dropped one.

The generator rejects a `bool` return for a notification method
(`RSG1006: Service method must return Task<RpcResult<T>> or RpcStreamSession`).
Declaring the method as a call instead would add a correlation id and a deadline
to a fire-and-forget message.

The server needs the result: a rejected snapshot, heartbeat, chat relay, or
voxel edit is a peer failure, and silently dropping it desynchronizes the
client. `ServerPeer.CloseOnRejectedSend` exists for this, and the server calls
`RpcPeer.Notify` directly at each site to get the boolean.

## Acceptance criteria

- [ ] The generator emits a notification proxy that returns the `RpcPeer.Notify` result.
- [ ] The server notification paths use the typed proxy again.
- [ ] A test pins that a rejected notification is observable through the proxy.

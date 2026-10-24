# Improvement: Make ClientHello a request/response call

- Type: improvement
- Status: open
- Workflow: ../specs/issues.md

## Problem

`ClientHello` is a one-way notification
(`WaywardBeyond.Networking/Rpc/IGameServerApi.cs`). The client sends it once from
`ClientRpcHost.Activate` and gets no acknowledgement. A dropped hello silently
leaves the peer on the default permission set. The server also logs nothing when
a peer joins without a claim.

The message carries only `UserId`. Later session work needs to exchange more
information at the same point, such as protocol capabilities and the bound
identity.

## Acceptance criteria

- [ ] `ClientHello` becomes a request with a response that confirms the claim.
- [ ] The client treats a missing response as a failed connection.
- [ ] The server logs a join with an anonymous or rejected claim.
- [ ] The response carries the session information later features need.
- [ ] The networking specs describe the call.

# Bug: The user claim is unauthenticated

- Type: bug
- Status: open
- Workflow: ../specs/issues.md

## Problem

`ClientHello { UserId }` is the only identity a client presents. The id is a
GUID that the client generates and keeps in `profile.toml`
(`WaywardBeyond.Client/Identity/ProfileUserClaimProvider.cs`). The server stores
it on `ServerPeer.Claim` without proof (`WaywardBeyond.Server/ServerHandlers.cs`).

Authorization keys on that id. Any remote peer that presents a user id granted a
group receives that group, including `admin`. In practice the secrecy of an
operator's GUID is the whole authorization model. The shipped
`assets/permissions/default.toml` grants nobody anything, so exposure starts
when an operator maps a user id to `admin`.

The policy also cannot tell a first-time user from an impersonated one, because
no state binds an id to a connection.

## Acceptance criteria

- [ ] A join proves ownership of the user id, or the server issues the identity.
- [ ] A client cannot obtain another user's permissions by presenting that id.
- [ ] `docs/specs/permissions.md` states the trust boundary and the mechanism.
- [ ] Tests pin the rejection of a forged or replayed claim.

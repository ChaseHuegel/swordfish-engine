# Improvement: Continue button connects to the last joined server

- Type: improvement
- Status: open
- Workflow: ../specs/issues.md

## Problem

Playtest: the Continue flow should join the last joined server (LAN or
remote), not just resume a local session. Today Continue always follows
the singleplayer/host path. With #0028 persisting the last-entered
endpoint, Continue can branch on how the last session was joined.

Locked design: the branch uses an explicit marker, not the endpoint -
singleplayer never writes the endpoint values, so they cannot encode the
mode. The marker lives in a new non-config settings file,
`ProfileSettings` (`profile.toml`), following the `Config<T>` +
`RegisterConfig` pattern, holding `LastServerMode` (enum, `Local` |
`Remote`, default `Local`). ProfileSettings is the intended home for
client-local state and preferences that are not real configuration
(future: saved servers from #0030, last character).

## Acceptance criteria

- [ ] `ProfileSettings` registered (`profile.toml`); `LastServerMode`
      written alongside the #0028 endpoint persistence: `Remote` on a
      remote connect/join, `Local` when the singleplayer/host path runs.
- [ ] Continue branches on `LastServerMode`: `Remote` ->
      `TransportManager.ConnectRemote` to the persisted endpoint + the
      normal join flow; `Local` -> the existing singleplayer/host path.
- [ ] Manual verification: end a session on a remote server, restart,
      Continue, and land in the same world; repeat for a local session.
- [ ] `docs/specs/config-schemas.md` documents `profile.toml` and
      `LastServerMode`, and `networking-join.md` documents the Continue
      branch (docs pass).
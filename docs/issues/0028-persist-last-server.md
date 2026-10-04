# Improvement: Persist the last-entered server address and port to network.toml

- Type: improvement
- Status: done
- Workflow: ../specs/issues.md

## Problem

Playtest: the user's last entered server address and port should survive
restarts. `NetworkingSettings.DefaultHost`/`DefaultConnectPort`
(`NetworkingSettings.cs:13-14`) already prefill the multiplayer page, but
nothing writes them back - the prefill is always the config default
(`127.0.0.1:7777`). Reusing these two keys as the "last used" endpoint
gives persistence with zero new config surface and directly feeds the
continue-to-last-joined work (#0029).

## Acceptance criteria

- [x] On a connect/join attempt, the entered address and port are written
      to `NetworkingSettings.DefaultHost`/`DefaultConnectPort` and
      persisted to `network.toml` (config save path, `SettingsManager`
      precedent).
- [x] The multiplayer page prefill reads the persisted values (fresh
      launch restores the last-entered endpoint).
- [x] `docs/specs/config-schemas.md` documents that these keys act as the
      last-used endpoint (docs pass).
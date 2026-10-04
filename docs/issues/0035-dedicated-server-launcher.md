# Improvement: Headless dedicated server launcher

- Type: improvement
- Status: open
- Workflow: ../specs/issues.md

## Problem

Playtest: add a headless dedicated server launcher. Serving any world today
requires the full game client (window, rendering, input) because the
authoritative server is hosted inside the client module.
`networking-transports.md` names a dedicated server as a goal, and research
#0019 assumes one.

## Locked design

Composition, not a mode flag: nothing needs to branch on "dedicated-ness".
`NetworkMode` stays as-is (Host/Client); it discriminates only whether this
client process also hosts (`ServerModule.cs:21-28`). A dedicated server is
simply a process that loads the server modules and none of the client
module - no UI, input, rendering, loopback, or client-world services are
registered. `ServerModule` already registers the server + `LanHost` in the
default (non-`--client`) mode, so it works unmodified.

- **Launcher.** New project `WaywardBeyond.Server.Launcher` (mirroring the
  client launcher structure): console `Exe` referencing the server and
  shared modules only - `WaywardBeyond.Server.Core`, `Shared.Networking`,
  `Shared.Data`, `Shared.Gameplay`, `Shared.Config`, `Shared.Bricks`,
  `Shared.Skills` - and not `WaywardBeyond.Client.Core`. `ServerContext`
  constructs its own `JoltPhysicsSystem` directly (`ServerContext.cs:69`),
  so no engine module is required.
- **Shared host composition lives inside Server.Core.** Server.Core is
  the module present in every hosting embedding and already owns
  `PersistentNatsProcess`/`Streaming`. The full host wire-up moves there
  (growing `ServerComposition` or a new `HostComposition` class within
  Server.Core): the `NsdMessageSerializer` set and `NetworkRegistry`
  init, `LocalConnection` + `ServerConnectionHub` delegate,
  `KeyValueStore` (`Injector.cs:92`) and `PersistentNatsProcess`,
  `NetworkingSettings` config, `LanHostInfo`, and every other service the
  server resolves that the client injector registers today (e.g. physics
  and config settings). The client module delegates its host
  registrations to the shared composition, so the embedded host and the
  dedicated launcher compose from the same source and cannot drift. The
  client keeps only client-only wiring (input, UI, loopback transport
  selection).
- **Lifecycle.** NATS start (per #0022's clean shutdown), `ServerContext`
  + `LanHost` + beacon; join-driven world loading only (no pre-load; the
  existing `ServerJoinSystem` loads levels on join); Ctrl+C/SIGTERM and
  console close run clean shutdown (world flush, session teardown, NATS
  stop).
- **CLI surface.** `--name`, `--port` overriding `NetworkingSettings`
  defaults.
- Out of scope: player auth, admin/console commands, per-client worlds
  (#0008), moderation.

## Acceptance criteria

- [ ] `WaywardBeyond.Server.Launcher` boots headless without a client
      module; LAN clients join and play through the normal TCP path
      (end-to-end verified).
- [ ] Host wiring extracted into the shared Server.Core composition; the
      client host path registers the same shared composition (no
      duplicated or drifted wire-up between embedded host and launcher).
- [ ] Clean shutdown (Ctrl+C, console close) flushes the world, ends
      sessions, and stops the NATS child (#0022).
- [ ] `networking-transports.md` gains a dedicated-server section
      documenting the launcher, module set, and lifecycle (docs pass).
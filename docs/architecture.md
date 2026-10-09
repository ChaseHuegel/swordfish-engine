# Swordfish Engine — Architecture

One subject: the as-built architecture. It covers the module map, the process
and world split, the persistence schema, and the CLI surface.

## Module map

| Directory/Project | Role | Framework | NuGet? |
|---|---|---|---|
| `Shoal/` | App host, DI (DryIoc), module loader, CLI, localization | `net8.0` | Shoal |
| `Swordfish/` | Engine module: rendering (Silk.NET/OpenGL), physics (Jolt), audio, input, UI | `net8.0` | Swordfish |
| `Swordfish.ECS/` | Struct-based ECS: Entity, ChunkedStore, World | `net8.0` | Swordfish.ECS |
| `Swordfish.Library/` | Shared types, serialization (Needlefish), DI abstractions | `netstandard2.1` | Swordfish.Library |
| `Swordfish.Integrations/` | Integrations (SQL, FontAwesome, etc.) | — | Swordfish.Integrations |
| `Swordfish.Compilation/` | Lexer/parser/linter for custom shader/script langs | `netstandard2.0` | Swordfish.Compilation |
| `Reef/` | Renderer-agnostic IMGUI library (alpha, replaces Dear ImGui) | `net8.0` | Reef |
| `Shoal.Extensions.Swordfish/` | Shoal extensions specific to Swordfish | `net8.0` | — |
| `Swordfish.Launcher/` | Dev launcher for Swordfish modules | `net9.0` | — |
| `Swordfish.Demo/` | Sandbox / tech demo module | — | — |
| `Swordfish.Editor/` | Visual editor module (inspector, hierarchy, file browser) | — | — |
| `WaywardBeyond.Client/` | Game client module | `net9.0` | — |
| `WaywardBeyond.Server/` | Game server module | `net9.0` | — |
| `WaywardBeyond.Data/` | Shared data models (client+server) | — | — |
| `WaywardBeyond.Config/` | Shared config types | — | — |
| `WaywardBeyond.Permissions/` | Shared dot-key permission policy and file loader | `net9.0` | — |
| `WaywardBeyond.Client.Launcher/` | Game client launcher app | — | — |
| `WaywardBeyond.Networking/` | Standalone networking layer over the ECS | `net9.0` | — |
| `WaywardBeyond.Gameplay/` | Shared gameplay: sim step, voxels, interactions, generation | `net9.0` | — |
| `WaywardBeyond.Skills/` | Shared skill module: definitions, headless loader, skill state | `net9.0` | — |
| `WaywardBeyond.Bricks/` | Shared brick module: definitions, headless database, id registry | `net9.0` | — |
| `WaywardBeyond.Bodies/` | Shared body module: definitions, headless database | `net9.0` | — |

**Entrypoints**: `Swordfish.Launcher/Program.cs` (`new SwordfishEngine(args).Run()`)
and `WaywardBeyond.Client.Launcher/Program.cs`.

## Core architecture principles

- **Shoal modularity**: every feature is a Shoal module. A module = a DLL +
  `manifest.toml` (ID, name, assemblies). Modules are auto-discovered and loaded
  via `Shoal/Modularity/ModulesLoader.cs`.
- **DI via DryIoc**: `IContainer` everywhere. Modules register services via
  `IDryIocInjector`. See `Shoal/DependencyInjection/`.
- **ECS-first**: game objects are entities composed of struct components,
  processed by systems (`Swordfish.ECS/`). Avoid OOP hierarchies.
- **Structs default** for new types, especially data types. Classes only for
  reference semantics or polymorphism.

## Engine vs game split

- **Engine set**: everything except `WaywardBeyond.*` (`Swordfish`,
  `Swordfish.ECS`, `Swordfish.Library`, `Swordfish.Integrations`,
  `Swordfish.Compilation`, `Shoal`, `Reef`, `Shoal.Extensions.Swordfish`,
  launchers, demo/editor).
- **Game set**: `WaywardBeyond.*`.
- Engine changes commit separately and first, never mixed with game commits.
  Change the separator only with justification.

## Process & world split

The game process runs one client world and N server worlds concurrently:

- **Client world** — `Swordfish/ECS/ECSContext.cs`, ticked on the `"ECS"`
  thread. Runs engine systems plus client gameplay systems.
- **Server worlds** — one per loaded level, ticked sequentially on the
  `"Server"` thread by `WaywardBeyond.Server/ServerWorldHost.cs`. Each
  world is a per-world DI graph (`ServerWorld.cs`) holding its own store,
  physics, hub, sessions, and systems, with idle unload. See
  [specs/networking-worlds](specs/networking-worlds.md).

The worlds communicate **only** through serialized nsd messages over a
transport. Singleplayer runs the authoritative server in-process on its own
thread over a `LocalConnection` loopback that exercises the full wire protocol.
The loopback rides the same world-routed join path as LAN peers. There is no
separate singleplayer simulation path.

`ServerModule` registers the server composition (see
[specs/networking-worlds](specs/networking-worlds.md) for the per-world DI
graph); the dedicated launcher is a thin host around the same wire-up.

See [specs/networking-overview](specs/networking-overview.md) for the full
process boundary detail.

## Persistence schema

All save data persists in SQLite databases through `Microsoft.Data.Sqlite`.
`StoragePaths` (`WaywardBeyond.Data/StoragePaths.cs`) resolves the
layout under `StorageSettings.DataRoot` (default `saves/`, file
`storage.toml`). The client owns `profile.db` (characters and save-listing
metadata). The server owns one database per level. The dedicated server can
override the data root with `--data`.

The source of truth for full details is [specs/persistence](specs/persistence.md).

### Levels

| Database | Owner | Tables |
|---|---|---|
| `saves/profile.db` | Client | `characters`, `save_meta` |
| `saves/<levelGuid>/level.db` | Server | `level`, `entities`, `character_locations` |

## CLI surface

The engine has a two-layer command/argument surface.

### Process arguments (Shoal)

`Shoal/CommandLine/` tokenizes `argv` into `CommandLineArgs`:
- `CommandLineParser.cs` lexes flags (`-x`, `--option`) and values, including
  quoted values.
- `CommandLineTokenParser.cs` reduces tokens into typed args.
- `--option=value` form is supported. See `CommandLineToken.cs`.

### In-game commands (Swordfish.Library)

`Swordfish.Library/IO/` defines a command abstraction:
- `Command.cs` — base `Command` + generic subcommand variants
  `Command<TSub0...>`.
- `CommandParser.cs` — prefix-indicator parsing + `TryRunAsync`.

Registration: `Shoal/AppEngine.cs` scans assemblies and registers commands via
`RegisterCommands` (lines ~305–319). No concrete game commands ship yet; the
only implementations are tests (`Swordfish.Tests/CommandTests.cs`).

## SQL integration (legacy/parallel)

`Swordfish.Integrations/SQL/` provides an optional SqlClient layer:
- `Database.cs` — SqlClient wrapper.
- `Query.cs` — fluent builder (`Select/From/Where/Equals/And/InsertInto/Update/Set/Columns/Values/End`).

This is not used by the game's save path, which uses SQLite directly in
`WaywardBeyond.Data`.

## Notable dependencies

- **Silk.NET** 2.22.0 — OpenGL, windowing, ImGui
- **JoltPhysicsSharp** 2.17.5 — 3D physics
- **DryIoc** 5.3.3 — DI container
- **Needlefish** 1.2.0 — binary serializer (custom)
- **Microsoft.Data.Sqlite** 8.0.6 — save store
- **Currents/CRNT** — UDP protocol (custom)
- **Tomlet** 6.2.0 — TOML parsing
- **SmartFormat.NET** — localization formatting
- **ImageSharp** — image loading
- **SoundFlow** — audio
- **msdf-atlas-gen** — font atlases (external tool)
# Swordfish Engine — Agent Guide

Compact reference for AI agents working in this repo. Read the linked docs
before working; each covers one subject. Start at [docs/README.md](docs/README.md).

The prose in this repo's docs uses Simplified Technical English: active voice,
short sentences, no semicolons, no contractions.

## Build & test

```bash
dotnet build                                        # Debug default; runs nsdc codegen
dotnet test Swordfish.Tests                         # xunit engine tests
dotnet test WaywardBeyond.Client.Tests              # NUnit game tests
dotnet test Reef.Tests                              # xunit, net8.0-windows (Windows only)
dotnet test Reef.Text.Tests                         # xunit Reef rich-text parser, cross-platform
dotnet pack -o ./.packages/                         # Swordfish, Swordfish.Integrations, Swordfish.Library
dotnet run --project Reef.Benchmarks                # BenchmarkDotNet
```

- `Reef.Tests` targets `net8.0-windows`; it cannot run on Linux.
- `Reef.Text.Tests` is `net8.0`; it runs on Linux.
- No CI workflows exist (`.github/workflows/` is empty).
- No lint or typecheck scripts. `dotnet build` is the check.
- Stale `launch.json` references `Swordfish/bin/Debug/Swordfish.exe`; the real
  launcher is `Swordfish.Launcher`.

## Repository index

Every top-level path and what it is:

| Path | What it is |
|---|---|
| `Shoal/` | App host: DI (DryIoc), module loader, CLI, localization |
| `Shoal.Build/` | MSBuild logic for building/publishing Shoal modules |
| `Shoal.Extensions.Swordfish/` | Shoal extensions specific to Swordfish |
| `Swordfish/` | Engine module: rendering, physics, audio, input, UI |
| `Swordfish.Compilation/` | Lexer/parser/linter for shader/script languages |
| `Swordfish.Demo/` | Sandbox / tech demo module |
| `Swordfish.ECS/` | Struct-based ECS: Entity, ChunkedStore, World |
| `Swordfish.ECS.Benchmarks/` | ECS BenchmarkDotNet benchmarks |
| `Swordfish.ECS.Generator/` | ECS source generators |
| `Swordfish.Editor/` | Visual editor module |
| `Swordfish.Integrations/` | Integrations (SQL, FontAwesome) |
| `Swordfish.Launcher/` | Dev launcher for Swordfish modules |
| `Swordfish.Library/` | Shared types, serialization (Needlefish), DI abstractions |
| `Reef/` | Renderer-agnostic IMGUI library (alpha) |
| `Reef.Benchmarks/` | Reef BenchmarkDotNet benchmarks |
| `Reef.Tests/` | Reef tests (Windows only) |
| `Reef.Text.Tests/` | Reef rich-text parser tests (cross-platform) |
| `WaywardBeyond.Bodies/` | Shared body module: definitions, headless database |
| `WaywardBeyond.Bricks/` | Shared brick module: definitions, headless database, id registry |
| `WaywardBeyond.Client/` | Game client module |
| `WaywardBeyond.Client.Launcher/` | Game client launcher |
| `WaywardBeyond.Client.Tests/` | Game client tests (NUnit) |
| `WaywardBeyond.Config/` | Shared config types |
| `WaywardBeyond.Data/` | Shared data models |
| `WaywardBeyond.Gameplay/` | Shared gameplay: sim step, voxels, interactions, generation |
| `WaywardBeyond.Networking/` | Standalone networking layer over the ECS |
| `WaywardBeyond.Permissions/` | Shared dot-key permission policy and file loader |
| `WaywardBeyond.Server/` | Game server module |
| `WaywardBeyond.Server.Launcher/` | Game server launcher |
| `WaywardBeyond.Skills/` | Shared skill module: definitions, headless loader, server skill state |
| `docs/` | Self-documenting doc system (subject index in `docs/README.md`) |
| `README.md`, `LICENSE` | Project docs |

## First stops

Route a goal to the right doc:

| I want to… | Read |
|---|---|
| Know the conventions before writing code | [docs/development.md](docs/development.md) |
| Understand the module/process architecture | [docs/architecture.md](docs/architecture.md) |
| Edit or add config schema | [docs/specs/config-schemas.md](docs/specs/config-schemas.md) |
| Define game content | [docs/specs/asset-definitions.md](docs/specs/asset-definitions.md) |
| Change save/persistence | [docs/specs/persistence.md](docs/specs/persistence.md) |
| Change networking | the networking specs under `docs/specs/` (see index) |

## Essential conventions

- **Shoal modularity.** Every feature is a Shoal module: a DLL + `manifest.toml`
  (ID, name, assemblies), auto-discovered and loaded via
  `Shoal/Modularity/ModulesLoader.cs`.
- **DI via DryIoc.** `IContainer` everywhere. Modules register via
  `IDryIocInjector`. See `Shoal/DependencyInjection/`.
- **ECS-first.** Game objects are entities composed of struct components,
  processed by systems (`Swordfish.ECS/`). Avoid OOP hierarchies.
- **Structs default** for new types, especially data types. Classes only for
  reference semantics or polymorphism.
- **Manifest files** require `CopyToOutputDirectory=Always`.
- **Sensitive serialization** uses `.nsd` files; generated `.cs` under
  `**/CodeGen/Output` must never be hand-edited. Config uses TOML.
- **Engine vs game.** Everything except `WaywardBeyond.*` is engine. Engine
  changes commit separately, first, standalone-green, never mixed with game.
- See [docs/development.md](docs/development.md) for the full style guide.

## Quality gates

Before you finish a change, run:

1. `dotnet build` — must compile clean.
2. `dotnet test Swordfish.Tests` — engine tests.
3. `dotnet test WaywardBeyond.Client.Tests` — game tests (cross-platform).
4. `dotnet test Reef.Tests` — Windows only; run when on Windows.

Then run the mandatory **docs pass** (see the next section). Stale docs are a
bug; do not finalize a change until the docs match the code.

## Docs pass (mandatory)

Every change set ends with a docs pass, run before finalizing. The full
protocol is in [docs/development.md](docs/development.md). In short:

1. Map the change to its doc via the subject index
   ([docs/README.md](docs/README.md)).
2. Update the matching doc, or create a new one in `docs/specs/`.
3. Refresh the source-of-truth `file:line` pointers.
4. Link instead of copying; never duplicate.

The pass is mandatory whenever the change touches a format, a correlation rule,
a DB bucket/table, a config key, or an API route.

## SDK requirements

- .NET 8 SDK minimum (some projects target net9.0; net8.0 is the baseline).
- Windows needed for `Reef.Tests` and the full engine runtime.

## Read next

Open [docs/README.md](docs/README.md) for the subject index.
# Swordfish Engine — Agent Guide

Compact reference for AI agents working in this repo. Every line is hard-earned context you'd likely miss reading only the code.

## Build & Test

```bash
# Build everything (Debug is default)
dotnet build
dotnet build --configuration Release

# Run tests (3 test projects, 2 frameworks)
dotnet test Swordfish.Tests          # xunit
dotnet test Reef.Tests               # xunit, net8.0-windows (Windows only)
dotnet test WaywardBeyond.Client.Core.Tests  # NUnit

# Pack NuGet packages (Swordfish, Swordfish.Integrations, Swordfish.Library)
dotnet pack -o ./.packages/
pwsh ./Pack.ps1                      # same as above, PowerShell wrapper

# Benchmarks (Reef only)
dotnet run --project Reef.Benchmarks  # BenchmarkDotNet
```

- **Reef.Tests** targets `net8.0-windows` — cannot run on Linux. The other test projects work cross-platform.
- No CI workflows exist (`.github/workflows/` is empty).
- No `global.json`, `Directory.Build.props`, or `.editorconfig` — each `.csproj` is self-contained.
- Stale `launch.json` references `Swordfish/bin/Debug/Swordfish.exe` — the real launcher is `Swordfish.Launcher`.

## Project Boundaries

| Directory | Role | Framework | NuGet? |
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
| `WaywardBeyond.Client.Core/` | Game client module | `net9.0` | — |
| `WaywardBeyond.Server.Core/` | Game server module | `net9.0` | — |
| `WaywardBeyond.Shared.Data/` | Shared data models (client+server) | — | — |
| `WaywardBeyond.Shared.Config/` | Shared config types | — | — |
| `WaywardBeyond.Client.Launcher/` | Game client launcher app | — | — |

**Entrypoints**: `Swordfish.Launcher/Program.cs` (`new SwordfishEngine(args).Run()`) and `WaywardBeyond.Client.Launcher/Program.cs`.

## Architecture Essentials

- **Shoal modularity**: Every feature is a Shoal module. A module = a DLL + `manifest.toml` (ID, name, assemblies). Modules are auto-discovered and loaded via `Shoal/Modularity/ModulesLoader.cs`.
- **DI via DryIoc**: `IContainer` everywhere. Modules register services via `IDryIocInjector`. Use `Shoal/DependencyInjection/`.
- **ECS-first**: Game objects are entities composed of struct components, processed by systems (`Swordfish.ECS/`). Avoid OOP hierarchies.
- **Structs default** for new types, especially data types. Classes only for reference semantics or polymorphism.
- **Manifest files** requiring `CopyToOutputDirectory=Always`: `manifest.toml` per module, `modules.toml` per launcher (defines load order).
- **Config** in TOML via Tomlet. See `Shoal/Modularity/ModuleOptions.cs`, `Shoal/Globalization/Language.cs`.

## Notable Dependencies

- **Silk.NET** 2.22.0 — OpenGL, windowing, ImGui
- **JoltPhysicsSharp** 2.17.5 — 3D physics
- **DryIoc** 5.3.3 — DI container
- **Needlefish** 1.2.0 — binary serializer (custom, https://github.com/ChaseHuegel/needlefish)
- **Currents/CRNT** — UDP protocol (custom, https://github.com/ChaseHuegel/Currents)
- **Tomlet** 6.2.0 — TOML parsing
- **SmartFormat.NET** — localization formatting
- **ImageSharp** — image loading
- **SoundFlow** — audio
- **msdf-atlas-gen** — font atlases (external tool)

## Conventions

- `ImplicitUsings` and `Nullable` enabled where the `.csproj` has them (most projects). `Swordfish.Library` has `Nullable` disabled.
- `AllowUnsafeBlocks` enabled in `Swordfish`, `Shoal`, `Swordfish.ECS`, `Swordfish.Library`, `Reef`.
- `LangVersion 12` used in projects targeting older frameworks.
- No lint, format, or typecheck scripts/commands — standard `dotnet build` handles compilation.
- **ECS dirty tracking**: store-mediated writes (`Alloc<T...>`, `AddOrUpdate`, `Entity.Add`) automatically mark the component dirty. In-place mutation through a query `ref` is NOT auto-detected — mutating systems must use `store.QueryRef<T...>(...)` (or extend `EntitySystemRef<T...>`) and go through the `Ref<T>` accessor's `Write` property, which marks the component dirty and returns a write `ref` (`Read` is `ref readonly` and compiler-enforced). Grab `ref T value = ref accessor.Write;` once at the top of a callback for terse writes. Explicit `store.MarkDirty<T>(entity)` is only needed for reference-content mutation (e.g. `VoxelComponent`, arrays/collections inside a component) where no `Ref<T>` write occurs. `QueryDirty<T>`/`QueryDirty<T1,T2>`/`QueryRemoved<T>` iterate matching dirty components WITHOUT clearing — callers must explicitly `ClearDirty<T>(entity)` (or `ClearDirty(type, entity)`). Component removals are auto-flagged so `QueryRemoved` can detect them; the component value is preserved on removal so `QueryRemoved` readers can access the last known data. Despawn replication: the server `NetworkReplicationSystem` collects `QueryRemoved<NetworkComponent>` entities into `WorldSnapshot.RemovedEntities`, which clients use to `Free` matching entities.

- **Networked components (automatic replication)**: `WaywardBeyond.Shared.Networking` is a standalone layer over the ECS; the engine is never modified. A game/networking component is networked by defining it as an nsd `message` (partial) and adding `[NetworkComponent(uuid, direction)]` + `IDataComponent` on a partial declaration — `nsdc -p` emits partials. `NetworkRegistry.Initialize(assemblies)` scans for these and registers each with a default `NsdComponentCodec<T>` (drives the generated `Serialize()`/`Deserialize`). Engine/third-party components (e.g. `TransformComponent`, `PhysicsComponent`) are registered explicitly via `NetworkRegistry.Register<T>(uuid, direction, codec)` from the game wiring, never from the engine. Each entry has a stable `Uuid` type-identity and a `NetworkDirection` (`ServerOwned` = authoritative, replicated server→client; `ClientOwned` = client-authored, e.g. `InputComponent`, replicated client→server). The server `NetworkReplicationSystem` publishes dirty ServerOwned components + despawns and applies inbound ClientOwned snapshots; the client `ClientReconcileSystem` applies authoritative snapshots + replays pending input; `ClientReplicationSystem` sends dirty ClientOwned components upstream. `ComponentSnapshot { Entity, TypeUuid, Payload }` packets are packed into `WorldSnapshot { TickNumber, LastProcessedInput, Components[], RemovedEntities[] }`. Note: the game is currently single-process — the client embeds the server systems and uses a `LocalConnection` where `IsLocal` short-circuits replication, so the peer (`TcpTransport`) path is not yet exercised.

## Test Quirks

- `Swordfish.Tests` uses xunit + `TestBase` abstract class with DryIoc `Container` setup/teardown. Test files like `TestFiles/` are copied to output.
- `Reef.Tests` requires fonts and image test files at `TestFiles/`. Windows-only due to `net8.0-windows` TFM and `System.Drawing.Common`.
- `WaywardBeyond.Client.Core.Tests` uses NUnit (not xunit). Tests voxel object processing.
- All can be run with `dotnet test <project>`.

## Style Guide

Read `STYLE_GUIDE.md` before writing code — it documents naming, formatting, braces, null handling, DI patterns, threading conventions, ECS idioms, and serialization conventions extracted from the codebase.

## SDK Requirements

- .NET 8 SDK minimum (some projects target net9.0, but net8.0 is the common baseline)
- Windows needed for `Reef.Tests` and full `Swordfish` engine runtime (Silk.NET OpenGL window)

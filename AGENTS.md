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
| `Swordfish.ECS/` | Struct-based ECS: Entity, ChunkedStore, World | `net5.0` | Swordfish.ECS |
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

## Test Quirks

- `Swordfish.Tests` uses xunit + `TestBase` abstract class with DryIoc `Container` setup/teardown. Test files like `TestFiles/` are copied to output.
- `Reef.Tests` requires fonts and image test files at `TestFiles/`. Windows-only due to `net8.0-windows` TFM and `System.Drawing.Common`.
- `WaywardBeyond.Client.Core.Tests` uses NUnit (not xunit). Tests voxel object processing.
- All can be run with `dotnet test <project>`.

## SDK Requirements

- .NET 8 SDK minimum (some projects target net9.0, but net8.0 is the common baseline)
- Windows needed for `Reef.Tests` and full `Swordfish` engine runtime (Silk.NET OpenGL window)

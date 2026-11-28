# Swordfish Engine — Agent Guide

Compact reference for AI agents working in this repo. Read the linked docs
before working; each covers one subject. Start at [docs/README.md](docs/README.md).

The prose in this repo's docs uses Simplified Technical English: active voice,
short sentences, no semicolons, no contractions.

## Build & test

```bash
dotnet restore                                      # run on fresh checkout or after .csproj changes
dotnet build --no-restore                           # Debug default, runs Needlefish codegen
dotnet build Tests.slnf --no-restore                # build only test projects and their dependencies
dotnet build Engine.slnf --no-restore               # build only engine projects and engine tests
dotnet build Game.slnf --no-restore                 # build only Wayward Beyond projects and engine deps
dotnet build Tools.slnf --no-restore                # build Asset Studio, CLI, AssetTools, Meshing, and tool tests
dotnet build <Project> --no-restore                 # scoped build during iteration
dotnet test <TestProject> --no-build --filter <X>   # scoped test during iteration
dotnet test Tests.slnf --no-build                   # all 8 xunit test projects in parallel
dotnet test Tools.slnf --no-build                   # AssetTools and AssetStudio xunit test projects
```

Individual test projects (always pass `--no-build` after a build):

```bash
dotnet test Krill.Tests --no-build                  # standalone ECS package tests
dotnet test Swordfish.Tests --no-build              # xunit engine tests
dotnet test Reef.Tests --no-build                   # xunit Reef UI and render tests, cross-platform
dotnet test Reef.Text.Tests --no-build              # xunit Reef rich-text parser, cross-platform
dotnet test Reef.Generator.Tests --no-build         # xunit Reef markup generator, cross-platform
dotnet test Reef.Markup.Tests --no-build            # xunit Reef markup runtime and visual gallery
dotnet test WaywardBeyond.Tests --no-build          # xunit game tests
dotnet test WaywardBeyond.Voxels.Tests --no-build   # xunit shared voxel container tests
dotnet test WaywardBeyond.AssetTools.Tests --no-build   # xunit AssetTools and CLI tests
dotnet test WaywardBeyond.AssetStudio.Tests --no-build  # xunit Asset Studio headless UI and GL tests
```

### Agent build and test rules

- Run `dotnet restore` only on a fresh checkout or after editing `.csproj` files or package references. Pass `--no-restore` on subsequent `dotnet build` commands.
- During iteration, scope `dotnet build` to the affected project or test project (`dotnet build WaywardBeyond.Client --no-restore` or `dotnet build Swordfish.Tests --no-restore`) instead of building the full solution.
- Always pass `--no-build` to `dotnet test` when a previous command already built the target. Use `--filter` to run only the relevant test class or method during iteration.
- For the final test gate, run one `dotnet test --no-build` command across the solution rather than 8 separate `dotnet test <Project>` invocations.
- Pass `-p:SkipShoalModuleStaging=true` when building the full solution only for compilation checks without running a launcher. Leave module staging enabled when testing or launching `WaywardBeyond.Client.Launcher` or `WaywardBeyond.Server.Launcher`.

- `Reef.Tests` is `net8.0`; it runs on Linux. On non-Windows platforms the
  MSDF atlas fixtures in `Reef.Tests/TestFiles/Fonts` must exist, because the
  `msdf-atlas-gen` tool is Windows-only.
- `Reef.Text.Tests` is `net8.0`; it runs on Linux.
- `Reef.Generator.Tests` is `net8.0`; it runs the Roslyn generator in memory.
- `Reef.Markup.Tests` is `net8.0`; it renders the visual gallery with the
  pre-generated font atlases.
- `Swordfish.Tests` is `net9.0`; it references only engine projects.
- `WaywardBeyond.Tests` is `net9.0`; it holds the game tests and references the
  game modules.
- `WaywardBeyond.Voxels.Tests` is `net9.0`; it references only the voxel module
  and the brick module, so it runs without the engine.
- CI publishes game release artifacts from `.github/workflows/waywardbeyond.yml`
  and Asset Studio bundles from `.github/workflows/asset-studio.yml`.
- A strict `vX.X.X` tag publishes a GitHub Release with all four archives: game
  and Asset Studio for `win-x64` and `linux-x64`. A suffixed tag such as
  `v0.1.0-alpha-rc26` uploads workflow artifacts only.
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
| `Krill/` | Struct-based ECS: Entity, ChunkedStore, World (standalone package) |
| `Krill.Benchmarks/` | ECS BenchmarkDotNet benchmarks |
| `Krill.Generator/` | ECS source generators |
| `Krill.Tests/` | xunit tests for the Krill package |
| `Swordfish.Editor/` | Visual editor module |
| `Swordfish.Integrations/` | Integrations (SQL, FontAwesome) |
| `Swordfish.Launcher/` | Dev launcher for Swordfish modules |
| `Swordfish.Library/` | Shared types, serialization (Needlefish), DI abstractions |
| `Reef/` | Renderer-agnostic IMGUI library (alpha) |
| `Reef.Benchmarks/` | Reef BenchmarkDotNet benchmarks |
| `Reef.Generator/` | Roslyn generator for `.rml` markup and `.rss` styles |
| `Reef.Generator.Tests/` | Parser, diagnostics, XSD, binding, and emission tests |
| `Reef.Markup.Tests/` | Runtime, interaction, component, and visual gallery tests |
| `Reef.Markup.TestComponents/` | Shared component and widget fixtures for cross-assembly tests |
| `Reef.Tests/` | Reef UI and render tests (cross-platform) |
| `Reef.Text.Tests/` | Reef rich-text parser tests (cross-platform) |
| `WaywardBeyond.AssetStudio/` | Avalonia 11 desktop Asset Studio app, forms, search, and GL preview |
| `WaywardBeyond.AssetStudio.Tests/` | Headless Avalonia UI, visual capture, integration, and software GL tests |
| `WaywardBeyond.AssetTools/` | Lossless Tomlyn CST editor, drafts, atomic saves, validation, packs, blueprints |
| `WaywardBeyond.AssetTools.Cli/` | Asset Tools CLI (`validate`, `roundtrip`, `format`, `new pack`, `ids`) |
| `WaywardBeyond.AssetTools.Tests/` | xunit tests for AssetTools, round-trip suite, validation, packs, blueprints, CLI |
| `WaywardBeyond.Bodies/` | Shared body module: definitions, headless database |
| `WaywardBeyond.Bricks/` | Shared brick module: definitions, headless database, id registry |
| `WaywardBeyond.Client/` | Game client module |
| `WaywardBeyond.Client.Launcher/` | Game client launcher |
| `WaywardBeyond.Config/` | Shared config types |
| `WaywardBeyond.Data/` | Shared data models, nsd schemas, sqlite storage, localization |
| `WaywardBeyond.Gameplay/` | Shared gameplay: sim step, networked staging state, body config, interactions, save codec |
| `WaywardBeyond.Items/` | Shared item module: definitions, headless database, item-to-brick resolution |
| `WaywardBeyond.Meshing/` | Shared CPU voxel meshing, `PreviewMesh`, `ITextureLayerLookup`, region builder |
| `WaywardBeyond.Voxels/` | Shared voxel model: chunked container, sampling, orientations |
| `WaywardBeyond.Networking/` | Game networking layer: wire contracts, registry, codecs, transports |
| `WaywardBeyond.Permissions/` | Shared dot-key permission policy and file loader |
| `WaywardBeyond.Server/` | Game server module |
| `WaywardBeyond.Server.Launcher/` | Game server launcher |
| `WaywardBeyond.Skills/` | Shared skill module: definitions, headless loader, server skill state |
| `WaywardBeyond.Tests/` | Game tests: client, server, gameplay, networking, and game UI |
| `tools/` | Self-contained folder packaging scripts for Asset Studio and CLI |
| `docs/` | Self-documenting doc system (subject index in `docs/README.md`) |
| `userdocs/` | Human onboarding docs, examples, and quick references |
| `README.md`, `LICENSE` | Project docs |

## First stops

Route a goal to the right doc:

| I want to… | Read |
|---|---|
| Know the conventions before writing code | [docs/development.md](docs/development.md) |
| Learn Reef Markup | [userdocs/reef/README.md](userdocs/reef/README.md) |
| Understand the module/process architecture | [docs/architecture.md](docs/architecture.md) |
| Edit or add config schema | [docs/specs/config-schemas.md](docs/specs/config-schemas.md) |
| Define game content | [docs/specs/asset-definitions.md](docs/specs/asset-definitions.md) |
| Work on or package the asset tooling | [docs/specs/asset-tooling.md](docs/specs/asset-tooling.md) |
| Author 3D models, textures, or icons | [docs/specs/asset-art-style.md](docs/specs/asset-art-style.md) |
| Change save/persistence | [docs/specs/persistence.md](docs/specs/persistence.md) |
| Change an nsd message or generated model | [docs/specs/needlefish-codegen.md](docs/specs/needlefish-codegen.md) |
| Change networking | the networking specs under `docs/specs/` (see index) |
| Review a change before finishing | [docs/specs/code-review.md](docs/specs/code-review.md) |

## Essential conventions

- **Shoal modularity.** Every feature is a Shoal module: a DLL + `manifest.toml`
  (ID, name, assemblies), auto-discovered and loaded via
  `Shoal/Modularity/ModulesLoader.cs`.
- **DI via DryIoc.** `IContainer` everywhere. Modules register via
  `IDryIocInjector`. See `Shoal/DependencyInjection/`.
- **ECS-first.** Game objects are entities composed of struct components,
  processed by systems (`Krill/`). Avoid OOP hierarchies.
- **Structs default** for new types, especially data types. Classes only for
  reference semantics or polymorphism.
- **Manifest files** require `CopyToOutputDirectory=PreserveNewest`.
- **Sensitive serialization** uses `.nsd` files under
  `WaywardBeyond.Data/Nsd`. The `Needlefish.Generators` package emits the
  generated types at compile time. Never hand-edit generated source. Config
  uses TOML.
- **Engine vs game.** Everything except `WaywardBeyond.*` is engine. Engine
  changes commit separately, first, standalone-green, never mixed with game.
- **Krill is standalone.** `Krill/` references no other repo project and is
  published as its own package.
- **Less is more.** Prefer deletion over addition. No thin wrappers, no
  unearned abstractions.
- **Expected failure returns `Result<T>`.** Do not throw, and do not add a
  `bool TryGet(out T)` on a non-hot-path API.
- **One top-level type per file.** Never declare multiple top-level types in one
  `.cs` file. Nested types inside an owning type are allowed.
- **No `TODO`/`FIXME`/`HACK`.** Capture the work as an issue under
  `docs/issues/`.
- **Contract-only docs.** One-line summaries, `<inheritdoc/>` on
  implementations, no `docs/` references in code.
- See [docs/development.md](docs/development.md) for the full style guide.

## Quality gates

Before you finish a change, run:

1. `dotnet build --no-restore` (or `dotnet build` if `.csproj` files changed) — must compile clean.
2. `dotnet test --no-build` — runs all 8 test projects (`Krill.Tests`, `Swordfish.Tests`, `Reef.Tests`, `Reef.Text.Tests`, `Reef.Generator.Tests`, `Reef.Markup.Tests`, `WaywardBeyond.Tests`, `WaywardBeyond.Voxels.Tests`) in parallel.
3. Self-review against [docs/specs/code-review.md](docs/specs/code-review.md).
   Fix blocking findings. Capture the rest as issues.

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
- Windows needed for the full engine runtime (Silk.NET OpenGL window). The
  `msdf-atlas-gen` font tool is Windows-only, so other platforms reuse
  pre-generated atlas `.png` and `.json` files.

## Read next

Open [docs/README.md](docs/README.md) for the subject index.
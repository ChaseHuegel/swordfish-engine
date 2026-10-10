# Swordfish Engine — Development

One subject: how to work in this repository. It covers the toolchain, build and
test workflow, code style, commit rules, config-schema conventions, and the
docs pass. It folds in what used to live in root `STYLE_GUIDE.md` and
`COMMITS.md`.

## Toolchain

- .NET 8 SDK minimum. Some projects target `net9.0`. Net8.0 is the common baseline.
- Windows is needed for `Reef.Tests` and the full `Swordfish` engine runtime
  (Silk.NET OpenGL window).
- No `global.json`, `Directory.Build.props`, or `.editorconfig` exist. Each
  `.csproj` is self-contained.
- No lint, format, or typecheck scripts. Standard `dotnet build` is the check.

## Build & test

```bash
dotnet build                                        # Debug default; runs nsdc codegen
dotnet build --configuration Release
dotnet test Swordfish.Tests                         # xunit engine tests, cross-platform
dotnet test WaywardBeyond.Client.Tests         # NUnit game tests
dotnet test Reef.Tests                              # xunit, net8.0-windows (Windows only)
dotnet pack -o ./.packages/                         # Swordfish, Swordfish.Integrations, Swordfish.Library
pwsh ./Pack.ps1                                     # same as above
dotnet run --project Reef.Benchmarks                # BenchmarkDotNet
```

- `Reef.Tests` targets `net8.0-windows` and cannot run on Linux.
- No CI workflows exist (`.github/workflows/` is empty).
- `nsdc` codegen runs automatically during build, driven by Exec targets in each
  `.csproj` (e.g. `WaywardBeyond.Data.csproj`, `WaywardBeyond.Networking.csproj`).

## Code style

These are observed conventions extracted from the codebase. Follow them.

### Names & formatting
Conventions for common type patterns such as assets, config, and ECS live in [specs/naming](specs/naming.md).

| Element | Convention | Example |
|---|---|---|
| Namespace | File-scoped | `namespace Swordfish.ECS;` |
| Classes, structs, records | PascalCase | `SwordfishEngine`, `Entity` |
| Interfaces | PascalCase + `I` prefix | `IDataComponent`, `IEntryPoint` |
| Methods | PascalCase | `OnWindowLoaded()`, `TryGet<T>()` |
| Properties | PascalCase | `MaxValue`, `Container` |
| Public fields | PascalCase | `TargetTickRate` (rare) |
| Private fields | `_` + camelCase | `_args`, `_dataStore` |
| Private constants | `UPPER_SNAKE_CASE` | `FILE_MODULE_OPTIONS`, `SOLID_VOXEL` |
| Parameters | camelCase | `delta`, `expectedState` |
| Locals | camelCase, `var` when obvious | `var options` |
| Enums | PascalCase for type and members | `LayoutDirection.Vertical` |

Prefer domain-meaningful names for backing stores over generic ones:
`_billboards` / `_seenOwners`, not `_slots` / `_seen`. Pure computations use
`Get*` (e.g. `GetFallbackFacing`); create-or-update orchestrators use
`AddOrUpdate*` (e.g. `AddOrUpdateBillboard`).

Keep-as-is abbreviations (registered in `.DotSettings`): `API`, `ECS`, `GL`,
`ID`, `IO`, `IP`, `ISO`, `KVP`, `UI`, `UID`. One exception: `BehaviorState`
uses all-caps members (`RUNNING`, `SUCCESS`, `FAILED`) — scoped to that type.

### Braces & layout

- **Allman style** — opening brace on its own line for all blocks.
- Single-line accessors and lambdas use expression bodies (`=>`) when they fit
  on one line.
- Simple methods use braces even on one line unless they are a lambda.
- All blocks must have braces (if, for, while, etc.).
- With `ImplicitUsings` enabled, omit redundant `using` directives. With it
  off (e.g. `Swordfish.Library`), list them explicitly.

### Language features

- **`this.`** — never qualify, even for disambiguation. Use a different parameter name.
- **`readonly`** — on every field set once (constructor or inline).
- **`sealed`** — on utility/leaf classes not designed for inheritance.
- **`in` parameter modifier** — on value types passed to constructors and methods.
- **`internal` access** — prefer on types by default; make public only for a
  specific cross-assembly need.
- **Nullable reference types** — enabled by default. `Swordfish.Library` has it
  disabled; add `#nullable enable` to new/edited code there as a legacy exception.
- **Primary constructors** — preferred for small value/state types. Promote
  fields explicitly, e.g. `public readonly Uuid Owner = owner;`.
- **Collection expressions** (C# 12 `[]`) preferred over `new List<T>()`.
- Use named arguments when a value's meaning is not self-evident.

### Null handling

- `== null` / `!= null` for checks (not `is null` / `is not`).
- `?.` for null-conditional invocation on delegates and events.
- `??` / `??=` for coalescing.
- `?? throw` for guard clauses in constructors.

### Types

- **Structs**: ECS components (`: IDataComponent`), small data holders, math
  types (`Vector3`, `IntRect`).
- **Classes**: services, managers, long-lived objects, DI types.
- **`readonly struct`**: immutable value types (ECS components, result types).
- **`readonly ref struct`**: accessor / stack-only types (`Ref<T>`).
- **`partial`**: where generated or source-extended code augments a type.
- **Records**: allowed on net8.0+. Forbidden on the netstandard2.0/2.1 targets
  (`Swordfish.Library`, `Swordfish.Compilation`). Prefer `readonly record struct`
  for immutable value data.
- One type per file unless nested within another type.
- Use the most concrete type that fits. Reach for an abstraction only when the
  value is real: a public API prone to churn, multiple implementations or
  sources, or genuinely heterogeneous data. Prefer near-concrete types
  (`T[]`, `List<T>`, `IReadOnlyList<T>`, `IReadOnlyCollection<T>`) over
  `IEnumerable<T>` as a declared type. Do not abstract to wrap a collection
  that stays private or internal, and never expose a collection a caller can
  mutate (a database must not hand out a mutable list). Avoid abstractions that
  add allocation or virtualization on hot paths.

### DI & architecture

- **DryIoc** everywhere. Modules register in `IDryIocInjector.Inject(IContainer)`.
- Constructor injection for all dependencies. `in` modifier on DI parameter types.
- `IContainer` resolved in infrastructure only (factories, app startup).
- `IAutoActivate` is a marker for service auto-activation on startup.
- Tests use `TestBase` with a fresh `Container` per test class.

### Threading

- `lock` for monitor-based synchronization.
- `ConcurrentQueue<T>` for producer-consumer patterns.
- `volatile` on boolean flags (`_stop`, `_pause`).
- `Interlocked.Exchange` for atomic field swaps.

### Exceptions

- Guard clauses with `?? throw` or early `throw new InvalidOperationException`.
- No try/catch for expected control flow — use `Result<T>`.
- Outer try/catch at module boundaries for logging + graceful degradation.
- `FatalAlertException` for fatal startup failures.

### Testing

- **xunit** for `Swordfish.Tests` and `Reef.Tests`.
- **NUnit** for `WaywardBeyond.Client.Tests`.
- Descriptive PascalCase test names: `SetMaxValueDoesScale`, `LightPropagationTest`.
- Arrange/Act/Assert structure.
- xunit tests inherit `TestBase` for DI container + `ITestOutputHelper`.

### Comments & docs

- XML docs are rare; add a terse one-line summary on public API or non-obvious
  private helpers. Content indented four spaces (`///     text`).
- Inline comments use `//` with two spaces (`//  text`) or a tab (`//\ttext`).
- Single-line action comments lead with an imperative verb, no trailing period.
- `// ReSharper disable` / `// ReSharper restore` for targeted suppression.

### ECS

- Components are `readonly struct` implementing `IDataComponent`.
- Generic constraints: `where T1 : struct, IDataComponent`.
- Systems inherit `EntitySystem` and override `Tick()`.
- Prefer `TryGet<T>()` / `out _` over `Has<T>()` + `Get<T>()`.
- Query modes: read-only `Query` (`in T`) vs read-write `QueryRef`
  (`ref Ref<T>`).
- A system's `IForEach` action, data record, and mutable state type are nested
  inside the owning system.

### ECS dirty tracking

- Store-mediated writes (`Alloc<T...>`, `AddOrUpdate`, `Entity.Add`) auto-mark a
  component dirty.
- In-place mutation through a query `ref` is NOT auto-detected. Mutating systems
  must use `store.QueryRef<T...>(...)` (or extend `EntitySystemRef<T...>`) and go
  through the `Ref<T>` accessor's `Write` property, which marks dirty and returns
  a write `ref` (`Read` is `ref readonly`, compiler-enforced). Grab
  `ref T value = ref accessor.Write;` once at the top of a callback.
- Explicit `store.MarkDirty<T>(entity)` is only needed for reference-content
  mutation (e.g. `VoxelComponent`, arrays/collections inside a component) where
  no `Ref<T>` write occurs.
- `QueryDirty<T>`/`QueryDirty<T1,T2>`/`QueryRemoved<T>` iterate matching dirty
  components WITHOUT clearing; callers must explicitly `ClearDirty<T>(entity)`.
- Component removals are auto-flagged so `QueryRemoved` detects them; the value
  is preserved on removal so readers access last-known data.

### Needlefish / codegen

- Sensitive serialization (networking, data storage) uses the Needlefish format
  (`.nsd` files). Configs use TOML.
- `.cs` files under `**/CodeGen/Output` are **auto-generated**. Never hand-edit
  them. Change the `.nsd` source instead.
- When adding/removing/renaming fields on a serializable type, edit the `.nsd`
  file, not the generated `.cs`.

### What not to do

- No XML docs on every member.
- No block-scoped namespaces (`namespace X { }`).
- Never qualify `this.`.
- No `is null` / `is not null` / `is { } foo`.
- No records in the netstandard2.0/2.1 projects.
- Do not disable Nullable in new projects.
- Do not introduce new test frameworks.
- Do not remove `CopyToOutputDirectory=Always` from manifest files.

## Commit rules

The repo uses terse, imperative, one-line commit messages.

- Keep the subject to a single line.
- Start with an imperative verb, capitalized: `Fix`, `Add`, `Implement`,
  `Remove`, `Move`, `Update`, `Use`, `Make`, `Clean up`, `Hook up`.
- Keep the rest lowercase and terse: `Add sqlite`, `Fix inventory not saving`.
- No prefixes (`feat:`/`fix:`/`chore:`), no scope parens, no emojis.
- No trailing period. Aim under ~60 characters.
- Be concrete: name the specific thing changed.
- Append issue numbers at the end of the subject, space-separated:
  `Fix cursor deltas on Wayland #590`.

Examples: `Fix sluggish Linux Wayland window resizes #591`,
`Add partial to nsd gen`, `Use KV for level saves`.

### Engine vs game commit boundary

- **Engine set**: everything except `WaywardBeyond.*` (`Swordfish`,
  `Swordfish.ECS`, `Swordfish.Library`, `Swordfish.Integrations`,
  `Swordfish.Compilation`, `Shoal`, `Reef`, `Shoal.Extensions.Swordfish`,
  launchers, demo/editor).
- **Game set**: `WaywardBeyond.*`.
- An `[E]` change is its own engine commit, committed **before** the game
  commits that consume it, and must build standalone.
- A mixed engine+game commit is never allowed.
- Game tasks must not hack around the engine, and must not request engine
  changes without generic justification.

## Config-schema conventions

- Any new tunable is a config key in a TOML config, never a hardcoded constant.
- Config is TOML via Tomlet. See `Shoal/Modularity/ModuleOptions.cs` and
  `Shoal/Globalization/Language.cs`.
- The exhaustive key lists live in
  [specs/config-schemas](specs/config-schemas.md) and
  [specs/asset-definitions](specs/asset-definitions.md).
- When you add a config key, update the matching schema table in the same change.

## Docs pass (mandatory)

Every change set ends with a docs pass, run before finalizing:

1. Map the changed behavior to its doc via the subject index
   ([docs/README.md](README.md)).
2. Update the matching doc, or create a new one if none covers the behavior.
   New docs go in `docs/specs/`; one subject per doc.
3. Refresh the source-of-truth `file:line` pointers so the next reader lands on
   the new code.
4. Do not document what is obvious. If a module docstring already states the
   fact, delete the doc copy and keep the pointer. Docs exist to make the
   non-obvious explicit.
5. Never duplicate content across docs. Link instead of copying.

The pass is mandatory (not optional) when the change touches a format, a
correlation rule, a DB table/bucket, a config key, or an API route.

Attribute the docs pass to the same change that caused it. Stale docs are a bug.

## Writing style

All new doc prose uses **Simplified Technical English**: one name per thing,
active voice, short sentences, no semicolons, no contractions.
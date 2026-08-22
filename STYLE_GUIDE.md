# Swordfish Engine — Style Guide

Extracted from the codebase. These are observed conventions, not aspirational rules. Follow them for consistency.

## Names & Formatting

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
| Locals | camelCase, `var` when type is obvious | `var options`, explicit for clarity |
| Enums | PascalCase for type and members | `LayoutDirection.Vertical` |

Keep-as-is abbreviations (registered in JetBrains `.DotSettings`): `API`, `ECS`, `GL`, `ID`, `IO`, `IP`, `ISO`, `KVP`, `UI`, `UID`.

One exception: `BehaviorState` uses all-caps members (`RUNNING`, `SUCCESS`, `FAILED`) — scoped to that type.

## Braces & Layout

- **Allman style** — opening brace on its own line for all blocks (class, method, `if`, `for`, `switch`, etc.).
- Single-line accessors and lambda expressions use expression bodies (`=>`) when they fit on one line.
- Simple methods use braces even when they fit on one line unless they are a lambda.
- All blocks must have braces (if, for, while, etc.).
- Projects with `ImplicitUsings` enabled omit redundant `using` directives (`Swordfish.ECS`, `Swordfish`); projects without it (e.g. `Swordfish.Library`) list them explicitly.

## Language Features

- **`this.`** — never qualify, even for disambiguation. Use a different parameter name instead.
- **`readonly`** — on every field that is set once (constructor or inline). Nearly universal.
- **`sealed`** — on utility/leaf classes that aren't designed for inheritance.
- **`in` parameter modifier** — on value types passed to constructors and methods (performance convention).
- **`internal` access** — prefer on types by default. Only make types public when there is a specific need for the API to be available to other assemblies or projects.
- **Nullable reference types** — enabled by default; all new code is nullable-aware. `Swordfish.Library` still has it disabled (`<Nullable>disable</Nullable>`) but this is a legacy maintenance exception, not the convention — new/edited code there should add `#nullable enable` until it can be migrated.
- **Primary constructors** — promoted to fields when captured, e.g. `BehaviorTree<TTarget>(in BehaviorNode root)` declaring `public readonly BehaviorNode Root = root;`.
- **Collection expressions** (C# 12 `[]`) preferred for initialization over `new List<T>()`.

## Null Handling

- `== null` / `!= null` for checks (not `is null` / `is not null`).
- `?.` for null-conditional invocation on delegates and events.
- `??` / `??=` for coalescing and coalescing assignment.
- `?? throw` for guard clauses in constructors.

## Types

- **Structs** for: ECS components (`: IDataComponent`), small data holders, math types (`Vector3`, `IntRect`), value wrappers.
- **Classes** for: services, managers, long-lived objects, dependency-injected types.
- **`readonly struct`** for immutable value types (ECS components, result types).
- **`readonly ref struct`** for accessor / stack-only types (`Ref<T>`).
- **`partial`** where generated or source-extended code augments a type (`Entity` uses `readonly partial struct`).
- **No record types** (record declarations don't work across the netstandard2.0/2.1 shared-library targets).

## DI & Architecture

- **DryIoc** everywhere. Modules register in `IDryIocInjector.Inject(IContainer)`.
- Constructor injection for all dependencies. `in` modifier on DI parameter types.
- `IContainer` resolved in infrastructure code only (factories, app startup). Domain code receives resolved interfaces directly.
- `IAutoActivate` as a marker interface for service auto-activation on startup.
- Tests use `TestBase` with a fresh `Container` per test class — register in `SetupContainer()`.

## Threading

- `lock` statements for monitor-based synchronization.
- `ConcurrentQueue<T>` for producer-consumer patterns.
- `volatile` on boolean flags (`_stop`, `_pause`).
- `Interlocked.Exchange` for atomic field swaps.

## Exceptions

- Guard clauses with `?? throw` or early `throw new InvalidOperationException`.
- No try/catch for expected control flow — use `Result<T>` struct instead.
- Outer try/catch at module boundaries for logging + graceful degradation.
- `FatalAlertException` for fatal startup failures (container validation, missing manifests).

## Testing

- **xunit** for `Swordfish.Tests` and `Reef.Tests`.
- **NUnit** for `WaywardBeyond.Client.Core.Tests`.
- Descriptive PascalCase test names: `SetMaxValueDoesScale`, `LightPropagationTest`.
- Arrange/Act/Assert structure.
- Tests inherit `TestBase` (xunit) for DI container + `ITestOutputHelper`.

## Comments & Docs

- XML doc comments are rare — add them only on public API surfaces where the behavior isn't obvious from the signature.
- Inline comments use `//` with two spaces (`//  text`) or with a tab (`//\ttext`).
- `// ReSharper disable` / `// ReSharper restore` for targeted warning suppression.

## ECS

- Components are `readonly struct` implementing `IDataComponent`.
- Generic constraints: `where T1 : struct, IDataComponent`.
- Systems inherit `EntitySystem` and override `Tick()`.
- Prefer `TryGet<T>()` / `out _` pattern over `Has<T>()` + separate `Get<T>()`.
- Query access modes: read-only `Query` (`in T`) vs read-write `QueryRef` (`ref Ref<T>`), mirroring the `Ref<T>` accessor and the `ForEachRef` delegate.

## Needlefish / CodeGen

- Sensitive serialization (networking, data storage) uses Needlefish format (`.nsd` files). Configs are the exception — use TOML.
- `.cs` files under `**/CodeGen/Output` are **auto-generated** from `.nsd` schemas. Never hand-edit them. Change the `.nsd` source instead.
- When adding/removing/renaming fields on a serializable type, edit the `.nsd` file, not the generated `.cs`.

## What Not to Do

- Do not add XML docs to every member — the codebase doesn't.
- Do not use block-scoped namespaces (`namespace X { }`).
- Do not qualify `this.` — ever.
- Do not use `is null` / `is not null` — use `== null` / `!= null`.
- Do not use records (incompatible with netstandard2.0 csproj targets).
- Do not disable Nullable in new projects — `Swordfish.Library` is a legacy exception; default to nullable-aware code.
- Do not introduce new test frameworks — stick with xunit for engine tests, NUnit for WaywardBeyond.
- Do not remove `CopyToOutputDirectory=Always` from manifest files.

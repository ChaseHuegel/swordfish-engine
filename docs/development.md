# Swordfish Engine — Development

One subject: how to work in this repository. It covers the principles, the
toolchain, build and test workflow, code style, commit rules, config-schema
conventions, and the docs pass. It folds in what used to live in root
`STYLE_GUIDE.md` and `COMMITS.md`.

## Principles

These principles rank above the style tables. When a rule and a principle
disagree, the principle wins, and the rule gets fixed.

- **Less is more.** Write the smallest change that fully solves the problem.
  Delete code before you add code.
- **Explicit over implicit.** A reader must see the intent at the call site.
  Do not hide work in getters, conversions, or side effects.
- **Few abstractions.** Abstract only for a real second implementation, a
  churn-prone public boundary, or genuinely heterogeneous data. A wrapper that
  only renames a call is noise.
- **Performance is a requirement.** Every per-tick path has a budget.
- **Allocation is a cost.** Prefer stack values, pooled buffers, `Span<T>`, and
  reuse. Do not allocate per entity per tick.
- **Hot paths get the strictest review.** ECS systems, replication,
  serialization, and render loops are hot paths.
- **Production ready.** This code is the foundation for a game. It must be
  correct under load, on bad input, and against a peer that lies.

Existing code does not meet every rule yet. Apply the rules when you touch a
file. Do not mass-rewrite untouched files.

## Toolchain

- .NET 8 SDK minimum. Some projects target `net9.0`. Net8.0 is the common baseline.
- Windows is needed for the full `Swordfish` engine runtime (Silk.NET OpenGL
  window). The `msdf-atlas-gen` font tool is Windows-only, so other platforms
  reuse pre-generated atlas `.bmp` and `.json` files.
- No `global.json`, `Directory.Build.props`, or `.editorconfig` exist. Each
  `.csproj` is self-contained.
- No lint, format, or typecheck scripts. Standard `dotnet build` is the check.

## Build & test

```bash
dotnet build                                        # Debug default; runs Needlefish codegen
dotnet build --configuration Release
dotnet test Swordfish.Tests                         # xunit engine tests, cross-platform
dotnet test Reef.Tests                              # xunit Reef UI and render tests, cross-platform
dotnet test Reef.Text.Tests                         # xunit Reef rich-text parser tests, cross-platform
dotnet test Reef.Generator.Tests                    # xunit Reef markup generator tests, cross-platform
dotnet test Reef.Markup.Tests                       # xunit Reef markup runtime and visual tests
dotnet test WaywardBeyond.Voxels.Tests              # xunit shared voxel container tests
dotnet pack -o ./.packages/                         # Swordfish, Swordfish.Integrations, Swordfish.Library
pwsh ./Pack.ps1                                     # same as above
dotnet run --project Reef.Benchmarks                # BenchmarkDotNet
```

- `Reef.Tests` targets `net8.0` and runs on Linux. Non-Windows runs reuse the
  pre-generated MSDF atlas fixtures in `Reef.Tests/TestFiles/Fonts`, because
  `msdf-atlas-gen` is Windows-only.
- Regenerate the atlas fixtures on Windows (or Wine) with the bundled
  generator. Set the font id from `FontInfo` as the output name:
  `Reef/Manifest/msdf-atlas-gen.exe -font "<font>" -imageout <id>.bmp
  -json <id>.json -chars "<charset>"`.
- CI publishes release artifacts from `.github/workflows/waywardbeyond.yml`.
- `Needlefish.Generators` runs as a Roslyn source generator during compilation.
  The repository does not run `nsdc` as a build step. All `.nsd` files live in
  `WaywardBeyond.Data/Nsd`. See [needlefish-codegen](specs/needlefish-codegen.md).

## Code style

These are observed conventions extracted from the codebase. Follow them.

### Names & formatting
Conventions for common type patterns such as assets, config, and ECS live in [specs/naming](specs/naming.md).

| Element | Convention | Example |
|---|---|---|
| Namespace | File-scoped | `namespace Swordfish.ECS;` |
| Classes, structs, records | PascalCase | `SwordfishEngine`, `Entity` |
| Interfaces | PascalCase + `I` prefix | `IDataComponent`, `IEntryPoint` |
| Methods | PascalCase | `OnWindowLoaded()`, `Get<T>()` |
| Properties | PascalCase | `MaxValue`, `Container` |
| Public fields | PascalCase | `TargetTickRate` (rare) |
| Private fields | `_` + camelCase | `_args`, `_dataStore` |
| Constants | `UPPER_SNAKE_CASE` | `FILE_MODULE_OPTIONS`, `SOLID_VOXEL` |
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

### Expressions & nesting

Deep nesting hides control flow and collapses a stack trace onto one line.
Keep the shape of the code readable.

- **Five levels is the limit.** Everything past four indents (16 spaces) in a
  method body is a signal to extract a private `Draw*`/`Read*` helper.
- **Never pass a multi-line argument to a condition.** An object initializer or
  a `with` expression inside an `if (...)` hides the test. Bind it to a local
  first:

  ```csharp
  Widgets.ActionButtonOptions options = _returnButton with { Rect = layout.ReturnButton };
  if (ui.ActionButton("Button_Back", label, options))
  ```

- **A lambda passed to a widget stays short.** Past ~10 lines, move the body to
  a named method and pass explicit state, so the lambda can be `static` and
  allocate nothing per row:

  ```csharp
  ui.SelectableCard(id, rect, options, context, static (contentUi, row) => DrawRow(contentUi, in row));
  ```

- **Extract per region, not per statement.** A page method draws one region
  (tabs, table, footer). `PixelRenderer` is the reference shape.
- **Pass a parameter object instead of a parameter list.** More than ~5
  arguments, or a run of `ref`/`out` flags, means a small `readonly record
  struct` context.
- **Prefer early-return guards** over wrapping the rest of a method in an `if`.

### Language features

- **`this.`** — never qualify, even for disambiguation. Use a different parameter name.
- **`readonly`** — on every field set once (constructor or inline).
- **`sealed`** — on utility/leaf classes not designed for inheritance.
- **`in` parameter modifier** — on value types passed to constructors and methods.
- **`internal` access** — prefer on types by default; make public only for a
  specific cross-assembly need.
- **Nullable reference types** — enabled by default. `Swordfish.Library` has it
  disabled; add `#nullable enable` to new/edited code there as a legacy exception.
- **Primary constructors** — preferred when they remove ceremony without
  obscuring field capture. Promote captured values to explicit `readonly` fields
  when stored or exposed, e.g. `public readonly Uuid Owner = owner;`.
- **Collection expressions** (C# 12 `[]`) preferred over `new List<T>()`.
- Use named arguments when a value's meaning is not self-evident.

### Null handling

- `== null` / `!= null` for checks (not `is null` / `is not`).
- `?.` for null-conditional invocation on delegates and events.
- `??` / `??=` for coalescing.
- `?? throw` for guard clauses in constructors.

### Type files

A file is named for the type it declares.

A generic type adds its type parameters to the file name after `Of`. This
follows the Microsoft convention for generic type names.

| Type | File |
|---|---|
| `Result<T>` | `ResultOfT.cs` |
| `IFilter<T>` | `IFilterOfT.cs` |
| `IAssetDatabase<TAsset>` | `IAssetDatabaseOfTAsset.cs` |
| `SwitchDictionary<TKey1, TKey2, TValue>` | `SwitchDictionaryOfTKey1TKey2TValue.cs` |

The type keeps its declared name. Only the file name carries the parameters.

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

### Performance & allocation

These rules bind every path and bind hardest to hot paths.

- No allocation in a per-tick path without a measured reason. This covers
  closures, boxing, LINQ, `params` arrays, interpolation, and iterator state
  machines.
- Prefer `for` over `foreach` when the collection is declared as an interface.
  A concrete `List<T>` enumerates without allocation.
- Prefer `T[]`, `List<T>`, `Span<T>`, and `stackalloc` with a fixed bound.
- Pool or reuse buffers. Return them on every exit path.
- Hoist invariants out of the loop. Do not recompute per entity what one call
  can compute.
- Gate expensive writes on a change check. See ECS dirty tracking.
- Measure before you claim a win. `Reef.Benchmarks` and
  `Swordfish.ECS.Benchmarks` exist for this.

### Placement & module boundaries

- Put a type in the project that owns the concern, not the project that
  consumes it. See the module map in [architecture](architecture.md).
- A shared type goes down into a shared project. A client-only or server-only
  type stays in that module.
- Depend downward only. The client must not reference the server, and a game
  project must not gain an engine dependency.
- A project that grows a second, unrelated concern splits into a new module.
  One clear responsibility is the trigger, not a line count.

### DI & architecture

- **DryIoc** everywhere. Modules register in `IDryIocInjector.Inject(IContainer)`.
- Each project owns its registrations through one composition entry. Embeddings
  call the entry instead of enumerating the project's types. See
  [specs/composition](specs/composition.md).
- Constructor injection for all dependencies. `in` modifier on DI parameter types.
- `IContainer` resolved in infrastructure only (factories, app startup).
- `IAutoActivate` is a marker for service auto-activation on startup.
- Tests use `TestBase` with a fresh `Container` per test class.

### Threading

- Know the thread. The client ECS ticks on the `"ECS"` thread. Server worlds
  tick on the `"Server"` thread. Rendering and input run elsewhere. See
  [architecture](architecture.md).
- Never touch a world's store from a foreign thread. Data crosses a thread
  boundary through the store, a queue, or an explicit hand-off.
- Assume no shared mutable state between systems unless the store mediates it.
- `System.Threading.Lock` for monitor-based synchronization. Never use an
  `object` or `this` as a lock target. `Lock` is reentrant.
- Never invoke external callbacks while holding a delivery lock. Queue the
  work, then drain it outside the lock.
- `ConcurrentQueue<T>` for producer-consumer patterns.
- `volatile` on boolean flags (`_stop`, `_pause`).
- `Interlocked.Exchange` for atomic field swaps.

### Errors and results

- Expected failure returns `Result` or `Result<T>`
  (`Swordfish.Library/Util/Result.cs`). Do not throw for a case the caller can
  handle.
- New non-hot-path APIs return `Result<T> Get(...)`, not
  `bool TryGet(..., out T)`.
- Keep `TryGet` where it matches a .NET idiom or a hot path:
  - `TryGetValue` on a dictionary-shaped type.
  - ECS store lookups and per-tick paths.
  - `TryParse`-style parsing.
  - Private helpers.
- Convert an existing `TryGet` only when you already touch it and the call site
  reads better. Never convert in bulk.
- A `Get` that returns a domain boolean is a query, not a failure state. Do not
  wrap it in `Result`.
- `Result<T>` converts implicitly to `T`. A failed result converts to
  `default`. Check `Success` before you read `Value`.
- Guard clauses with `?? throw` or an early `throw new InvalidOperationException`
  for programmer error and broken invariants.
- No try/catch for expected control flow.
- Outer try/catch at module boundaries for logging and graceful degradation.
- `FatalAlertException` for fatal startup failures.

### Testing

- **xunit** for `Swordfish.Tests`, `Reef.Tests`, `Reef.Text.Tests`, and
  `WaywardBeyond.Voxels.Tests`.
- Descriptive PascalCase test names: `SetMaxValueDoesScale`, `LightPropagationTest`.
- Arrange/Act/Assert structure.
- xunit tests inherit `TestBase` for DI container + `ITestOutputHelper`.
- `dotnet build` and the test projects must pass before you finalize. A red
  test is a red change.
- A test pins behavior, not an implementation. If a change intentionally
  removes or changes behavior, update or delete the matching test in the same
  change, and state it in the change summary.
- Delete a test that only covers removed code. Do not keep it alive with a
  rewrite it no longer needs.
- Decide which is wrong first: the test or the code. Never edit a test to match
  a bug.

### UI authoring

Read [specs/ui-design-system](specs/ui-design-system.md) before changing game UI.
Use `.rml` for structure and designer-facing copy. Use `.rss` for colors,
typography, and layout classes. Keep page services and navigation in the page
object. Keep view code-behind limited to localization, formatting, and event
forwarding.

### Comments & docs

Document the contract. Do not document the implementation.

- Write a one-line summary on each public, internal, or non-obvious type,
  member, and constructor. Exceed one line only when the contract is genuinely
  complex.
- `<inheritdoc/>` is required on an override, an interface implementation, and a
  member that refines a documented base member. Do not copy the base prose.
- State what the member does, what it requires, and what it returns or throws.
  Do not state how it works.
- A type summary states the purpose. It does not list the members.
- Do not reference plans, issues, `docs/` pages, or implementation decisions in
  code documentation. The code is the record. `file:line` pointers belong in
  `docs/`, never in XML comments.
- Delete a comment that restates code you can read in one line.
- Content is indented four spaces (`///     text`).
- Inline comments use `//` with two spaces (`//  text`) or a tab (`//\ttext`).
- Single-line action comments lead with an imperative verb, no trailing period.
- `// ReSharper disable` / `// ReSharper restore` for targeted suppression.
- No commented-out code. Delete it.
- No `TODO`, `FIXME`, or `HACK`. Capture the work as an issue. See
  [specs/issues](specs/issues.md).

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
- Change-gate hot-path writebacks. Read the current value first. Use `Write`
  only when the new value differs. A write of an unchanged value still marks the
  component dirty, so an idle system defeats delta replication and dirty polls.

### Needlefish / codegen

- Sensitive serialization (networking, data storage) uses the Needlefish format
  (`.nsd` files). Configs use TOML.
- All schemas live in `WaywardBeyond.Data/Nsd`. `Needlefish.Generators` emits
  the generated types during compilation. No generated `.cs` file is written to
  the source tree.
- When adding/removing/renaming fields on a serializable type, edit the `.nsd`
  file. See [needlefish-codegen](specs/needlefish-codegen.md).
- Do not wrap a generated nsd type in a record or model that copies its fields.
  Use the generated type at the boundary. Add a hand-written type only for
  behavior the schema cannot express.

### What not to do

- No prose XML docs on every member. Use `<inheritdoc/>` instead.
- No `TODO`/`FIXME`/`HACK` comments.
- No commented-out code.
- No new `bool TryGet(out)` on a non-hot-path API.
- No wrapper that only forwards a call.
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

Human onboarding docs live in `userdocs/reef/`. They follow the same writing
style and link to the formal docs.

Player-facing UI copy follows the Material style guide and a fixed language key
grammar. See [specs/ui-copy](specs/ui-copy.md) before you add or rename a
localized string.
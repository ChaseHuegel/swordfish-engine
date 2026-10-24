# Lua Scripting — Proposal and Development Plan

Status: proposed. Not implemented.

Author: opencode (deepseek-v4.1-flash).

Scope: the engine tier (a new `Swordfish.Scripting` project), the `Shoal`
loader seam, and the game binding hooks. Game content lands separately.

## 1. Summary

The engine has no scripting runtime. A loader stub exists, and it fails at
runtime. The documented intent is C# scripting. Compiled C# is full process
trust, so it cannot run untrusted community content.

A dedicated server will run scripts that the operator did not author. That
requirement decides the runtime. The proposal uses Lua through Lua-CSharp, an
interpreter with no native dependency and a capability sandbox.

The change has three parts:

1. A host and sandbox in a new engine project `Swordfish.Scripting`.
2. A loader seam in `Shoal` (`IScriptLoader`). Shoal takes no Lua dependency.
3. Bindings in modules. `Swordfish` binds engine and ECS. `WaywardBeyond.Gameplay`
   binds interactions and skills.

The runtime is swappable. The host sits behind an interface. A later change can
replace Lua with MoonSharp, NLua, or Jint without a change to game code.

## 2. Current state

### 2.1 The loader stub

`Shoal/Modularity/ModulesLoader.cs:80-90` computes a scripts path and then does
nothing:

```csharp
//  Compile any scripts into an assembly
PathInfo scriptsPath = manifest.ScriptsPath ?? directory.At("Scripts/");
if (scriptsPath.Exists())
{
    PathInfo[]? scriptFiles = scriptsPath.GetFiles(SearchOption.AllDirectories);
    if (options.AllowScriptCompilation && scriptFiles.Length > 0)
    {
        //  TODO implement script compilation
        logger.LogError("Unable to compile scripts in module {name} ({id}), script compilation is not supported.", manifest.Name, manifest.ID);
    }
}
```

### 2.2 The declared hooks

| Item | Location | Value |
|---|---|---|
| Script path | `Shoal/Modularity/ModuleManifest.cs:31-32` | `ScriptsPath`, default `<module>/Scripts/` |
| Gate | `Shoal/Modularity/ModuleOptions.cs:6-7` | `AllowScriptCompilation`, default false |
| Config schema | `docs/specs/config-schemas.md:22,44,49,55` | `ScriptsPath`, `AllowScriptCompilation` ("allow C# scripting") |

No `Scripts/` directory exists in any module. Every `modules.toml` sets
`AllowScriptCompilation = false`.

### 2.3 The stated vision

- `README.md:22` — Shoal provides "Dependency Injection, containerization, C#
  scripting, and a standardized module system".
- `README.md:31` — `Swordfish.Compilation` "will eventually provide
  implementations for C# scripting used by Shoal and Swordfish".
- `README.md:128-132` — the scripting roadmap marks Parser, Lexer, and Linter as
  done and Compiler as open.

### 2.4 The compilation framework

`Swordfish.Compilation` is a generic lexer, parser, and linter framework. It has
`Lexer<T>`, `TokenParser<TToken, TType>`, and `ILinter<T>`. It defines no
grammar, no AST, no emitter, and no language. Its only consumer is the CLI
parser in `Shoal/CommandLine`. The roadmap boxes describe the framework, not a
language.

### 2.5 Module loading today

`ModulesLoader.LoadModule` loads prebuilt DLLs with `Assembly.LoadFrom`
(`ModulesLoader.cs:106`). The load uses the default load context. There is no
isolation, no unload, and no hot reload. `HookAssembly` is also a stub
(`ModulesLoader.cs:131-135`).

### 2.6 Constraints

- **Determinism.** `docs/specs/networking-prediction.md:8` states that the
  gameplay simulation is a shared, deterministic step. The client predicts and
  reconciles. A script that touches the simulation must agree on the client and
  the server.
- **Trust.** A dedicated server runs community content. No sandbox exists. The
  dot-key permission model (`WaywardBeyond.Permissions`) is authorization, not
  code isolation.
- **No AOT.** No project sets `PublishAot` or `PublishTrimmed`. `Shoal` and
  `Swordfish` target `net8.0`. The game targets `net9.0`.

## 3. Goals and non-goals

Goals:

- Run untrusted Lua scripts from a module `Scripts/` directory.
- Remove ambient capabilities. A script sees only the host functions that the
  host grants.
- Bound each script with an instruction budget and a wall-clock deadline.
- Isolate faults. A broken script logs and does not stop the server.
- Keep scripting out of the predicted simulation step in v1.
- Provide a loader seam so Shoal does not depend on Lua.
- Let a module add scripts without a change to a core file.
- Keep the runtime swappable.
- Document the trust model and the determinism rules.

Non-goals:

- No scripting of the client-predicted physics step in v1.
- No component writes from scripts in v1. Bindings are read-only.
- No C# scripts. Compiled C# cannot be sandboxed.
- No visual scripting.
- No hot reload of the host or of a running script. A script reload is a
  separate change.
- No process isolation in v1. The design keeps a later out-of-process worker
  open.
- No Native AOT goal. The choice stays AOT-compatible.

## 4. Decisions

These decisions came from the research review.

| Decision | Choice |
|---|---|
| Runtime | Lua through Lua-CSharp (`LuaCSharp`, namespace `Lua`). Pure C#, MIT, no native dependency, AOT-safe. |
| Trust model | Capability sandbox plus resource budgets. Not an operating-system boundary. |
| Scope | Server-authoritative hooks and client event hooks. Not the predicted step. |
| Loader seam | An `IScriptLoader` interface in `Shoal`. The Lua host implements it in `Swordfish.Scripting`. |
| Bindings home | Modules. `Swordfish` binds engine and ECS. `WaywardBeyond.Gameplay` binds interactions and skills. |
| Thread model | One `LuaState` per world. Strict thread affinity. |
| Config key | Rename `AllowScriptCompilation` to `AllowScripting`. |
| Script location | `<module>/Scripts/`, extension `.lua`, recursive. |
| VM swap | The host is behind an interface. The VM is replaceable. |

## 5. Trust model

Lua-CSharp is an in-process interpreter. It is a capability sandbox. It is not
an operating-system boundary.

The proposal enforces three layers:

1. **Capability layer (v1).** The host grants a small set of functions. The
   sandbox denies `os`, `io`, `package`, `dofile`, `loadfile`, and raw CLR
   access. A script can call only what the host exposes.
2. **Resource layer (v1).** An instruction budget, a wall-clock deadline, and
   fault isolation bound each script.
3. **Process layer (later).** An optional out-of-process script worker for the
   most hostile tier. The design keeps this path open.

The documentation must state the limit. A defect in the interpreter or in a host
binding can escape the capability layer. The process layer is the answer for
hostile input.

## 6. Host architecture

### 6.1 Projects

- **`Swordfish.Scripting` (new).** Target `net8.0`. Owns the `LuaState`
  lifecycle, the sandbox platform, the budgets, and the binding registry.
  Composition entry `ScriptingComposition.RegisterScripting(this IContainer)`.
  This follows the one-entry-per-project rule in `docs/specs/composition.md`.
- **`Shoal`.** Gains the `IScriptLoader` interface and a loader seam. Shoal
  does not reference Lua.
- **`Swordfish`.** Registers engine and ECS bindings.
- **`WaywardBeyond.Gameplay`.** Registers interaction and skill bindings.

The host core has no reference to Lua types in its public surface. A thin
adapter holds the Lua-CSharp dependency.

### 6.2 Loader seam

`ModulesLoader.LoadModule` resolves an injected `IScriptLoader`. The steps are:

1. Compute the scripts path. Keep the current default, `<module>/Scripts/`.
2. If the path has no files, continue.
3. If `AllowScripting` is false, log and skip. Keep the current behavior.
4. If no `IScriptLoader` is registered, log an error. Keep the current
   "not supported" message as the fallback.
5. If a loader is registered, call it. The loader loads each `.lua` file into
   the module script host.

The loader returns a result. A script fault logs and does not fail the module.
A module with scripts and no assemblies already works. The loader returns early
at `ModulesLoader.cs:93`.

### 6.3 DI and composition

`AppEngine.AssemblyHookCallback` scans assemblies for `IDryIocInjector` and
invokes each (`Shoal/AppEngine.cs:324-341`). A module registers its bindings in
its own `Injector`, the same way `WaywardBeyond.Skills/Injector.cs` registers a
TOML parser and a database.

`Swordfish.Scripting` registers:

- The script host, as a singleton per world.
- The sandbox platform, built from the existing `VirtualFileSystem`.
- The binding registry, as a multi-value service (`AppendNewImplementation`).

### 6.4 Lifecycle

- Load on module load, after assembly hooking.
- Run `on_load()` once.
- Run `on_tick(dt)` on the owning thread each tick, within budget.
- Drop the `LuaState` on world teardown. Lua needs no collectible load context.
- Script state is ephemeral. The host rebuilds it from `on_load`. Saves do not
  carry script state.

### 6.5 Threading

Lua-CSharp states that `LuaState` is not thread-safe. The host enforces thread
affinity:

- **Server.** One `LuaState` per server world. The server thread ticks the
  world (`WaywardBeyond.Server/ServerWorld.cs:48-57`). Scripts run on that
  thread.
- **Client.** One `LuaState` for client scripts. The `"ECS"` thread ticks the
  client world (`Swordfish/ECS/ECSContext.cs:26-29`). Client scripts run on that
  thread.
- A host binding never calls into Lua from a second thread.

## 7. Sandbox and limits

### 7.1 Platform

Build a `LuaPlatform` for each host. Back the file system with the existing
`VirtualFileSystem`. Mount only the module content root. The host router sends
`print` and `log` to `ILogger`. The platform denies the operating-system
environment, the standard I/O, and the shell.

### 7.2 Denied surface

The host does not open the full standard library. It opens a curated subset. It
denies:

- `os` and `io`.
- `package` and `require` beyond the module loader.
- `dofile` and `loadfile`.
- `load` and `loadstring` for dynamic code from strings.
- Raw CLR access and reflection.

### 7.3 Budgets

| Limit | Mechanism | Default |
|---|---|---|
| Instructions per tick | `LuaState.SetHook` count hook | `ScriptInstructionBudgetPerTick` |
| Wall-clock per call | Hook deadline check plus cancellation token | `ScriptDeadlineMs` |
| Memory (advisory) | Best-effort heap check | `ScriptMemoryHintBytes` |

The hook raises a Lua error when the budget is spent. `pcall` in the host
catches the error. The host logs and stops the script for that tick.

Memory is advisory. Lua-CSharp uses the .NET garbage collector. The library
documents that a strict per-state memory limit is not possible. The
documentation must record this limit.

### 7.4 Fault isolation

The host wraps every entry into Lua in a `try`/`catch`. It catches
`LuaCompileException` and `LuaRuntimeException`. It logs the message, the
script file, and the line. The host does not stop the server. A script that
fails to compile is disabled with a log entry.

## 8. Determinism rules

Lua-CSharp implements Lua 5.2. Lua 5.2 has no integer subtype. All numbers are
doubles. Table iteration order is undefined. Both are determinism hazards.

The proposal scopes the hazard away:

- **v1 runs no script in the predicted physics step.** Scripts run as
  server-authoritative hooks and client event hooks. They do not take part in
  client prediction.
- **The host provides a seeded random number generator.** A script must not use
  raw `math.random`. The sandbox replaces `math.random` with the host RNG.
- **The host provides ordered iteration helpers.** The documentation records
  the undefined table order and the helper.
- **A script must not read the wall clock.** The host provides the sim tick and
  a fixed delta.

A later change that scripts the shared step must add a determinism test to
`Swordfish.Tests` first. The test must show identical state on two runs.

## 9. Script model and bindings

### 9.1 Script callbacks

A script defines these optional functions:

| Function | When | Scope |
|---|---|---|
| `on_load()` | Once at module load | Client or server |
| `on_tick(dt)` | Each tick, within budget | Client or server |
| `on_event(name, payload)` | On a named host event | Client or server |
| `on_interaction(ctx)` | On a server interaction | Server only |

### 9.2 Bindings

Bindings are `[LuaObject]`-wrapped C# types. The source generator builds the Lua
surface. This matches the source-generator pattern in `Swordfish.ECS.Generator`.

v1 bindings, read-only:

- **Log.** `log.info`, `log.warn`, `log.error`.
- **Config.** Read a value from the module TOML.
- **Random.** The seeded host RNG.
- **Time.** The sim tick and the fixed delta.
- **ECS read views.** A curated component set for one entity. Start with
  `IdentifierComponent`, `TransformComponent`, and `PhysicsComponent`.
- **Events.** Subscribe and publish named host events.
- **Commands.** Register an in-game command. Reuse `Swordfish.Library/IO/Command.cs`.

v2 bindings, deferred:

- ECS writes. Writes go through a queued command buffer on the owning thread.
  This respects the ECS dirty-tracking rules in `docs/development.md:168-183`.

### 9.3 Interaction hook

`WaywardBeyond.Gameplay/Interactions/IInteractionHandler.cs` is the existing
server mod hook. It runs after base validation and can replace the resolution.
`InteractionHandlerRegistry` holds the handlers and runs them in order
(`InteractionHandlerRegistry.cs:17-37`).

The binding wraps a Lua function into an `IInteractionHandler`. The context is
read-only. The handler returns one of `InteractionAction.Break`, `Place`, or
`None`. This reuses the existing seam and needs no new wire message.

## 10. Config and manifest

### 10.1 `modules.toml`

Rename the gate. Update the schema table in `docs/specs/config-schemas.md:49`
in the same change.

| Key | Type | Purpose | Default |
|---|---|---|---|
| `AllowScripting` | bool | allow Lua scripts | false |
| `ScriptInstructionBudgetPerTick` | int | Lua instructions per script per tick | 100000 |
| `ScriptDeadlineMs` | int | wall-clock per script call | 5 |
| `ScriptMemoryHintBytes` | int | advisory memory hint | 16000000 |

The old key `AllowScriptCompilation` is removed. The project is pre-release, so
this is a clean break.

### 10.2 `manifest.toml`

`ScriptsPath` stays. Its default stays `<module>/Scripts/`. No new manifest key
is required. A manifest with scripts and no assemblies is valid.

## 11. Integration

### 11.1 Client

`Swordfish.Scripting` registers a client script host. The `"ECS"` thread ticks
it. Events, commands, and UI hooks run there. Client scripts do not touch the
predicted step in v1.

### 11.2 Server

A server world is a per-world DI graph (`docs/specs/networking-worlds.md`). The
world resolves one script host. The server thread ticks it. The interaction
binding uses the existing registry.

A server system runs the tick. Add `ServerScriptSystem : IServerWorldSystem`.
Register it through `RegisterServerSystem<T>`
(`WaywardBeyond.Server/ServerComposition.cs:120-128`). Place it after the
interaction system in the documented tick order
(`ServerComposition.cs:29-30, 92-113`).

### 11.3 Failure isolation

A script fault must not stop a world. The script system catches every fault.
It logs and continues. This mirrors the fault handling in the RPC proposal.

## 12. Modding workflow

1. Add a `Scripts/` directory to the module.
2. Add a `.lua` file.

```lua
-- Scripts/greeter.lua
local players = 0

function on_load()
    log.info("greeter loaded")
end

function on_event(name, payload)
    if name == "player.join" then
        players = players + 1
        log.info("player joined, total " .. players)
    end
end
```

3. Enable scripting in `modules.toml`.

```toml
AllowScripting = true
LoadOrder = [
    "swordfish",
    "waywardbeyond.client",
    "my.module",
]
```

4. The module edits no core file. The module does not touch the loader, the
   composition, or a transport serializer.

## 13. Development plan

Each phase builds green and has its own commit. Phases 1 to 3 are engine tier.
Phase 4 adds game hooks and docs.

### Phase 1 — loader seam and host core

New files:

- `Shoal/Modularity/IScriptLoader.cs`
- `Swordfish.Scripting/Swordfish.Scripting.csproj`
- `Swordfish.Scripting/ScriptingComposition.cs`
- `Swordfish.Scripting/LuaScriptHost.cs`
- `Swordfish.Scripting/LuaSandbox.cs`
- `Swordfish.Scripting/ScriptPaths.cs`

Changed files:

- `Shoal/Modularity/ModulesLoader.cs` resolves `IScriptLoader`.
- `Shoal/Modularity/ModuleOptions.cs` renames the gate.
- `Shoal/Shoal.csproj` adds no Lua dependency.

Tests: loader seam, sandbox denial, budget interruption.

### Phase 2 — events, ticks, and read bindings

New files:

- `Swordfish.Scripting/Events/ScriptEventBus.cs`
- `Swordfish.Scripting/Bindings/LogBinding.cs`
- `Swordfish.Scripting/Bindings/TimeBinding.cs`
- `Swordfish.Scripting/Bindings/RandomBinding.cs`
- `Swordfish.Scripting/Bindings/EcsReadBinding.cs`
- `Swordfish.Scripting/ScriptBudget.cs`

Changed files:

- `Swordfish/EngineContainer.cs` registers the client script host and system.
- `Swordfish/ECS/` adds the client script system.

Tests: event delivery, tick budget, RNG determinism.

### Phase 3 — commands and interaction binding

New files:

- `Swordfish.Scripting/Bindings/CommandBinding.cs`
- `WaywardBeyond.Gameplay/Scripting/InteractionBinding.cs`
- `WaywardBeyond.Server/Systems/ServerScriptSystem.cs`

Changed files:

- `WaywardBeyond.Gameplay/Injector` registers the interaction binding.
- `WaywardBeyond.Server/ServerComposition.cs` registers `ServerScriptSystem`.

Tests: interaction hook replacement, command registration.

### Phase 4 — docs, config, and example

- Add `docs/specs/scripting.md`.
- Update `docs/specs/config-schemas.md` for the rename and the new keys.
- Update `docs/README.md`, `docs/architecture.md`, `docs/specs/composition.md`.
- Update `README.md` and `AGENTS.md`.
- Add `Swordfish.Demo/scripts/` with one example script.

## 14. Test plan

Tests live in `Swordfish.Tests` (`net9.0`). Add `Swordfish.Tests/Scripting/`.

`SandboxTests.cs`:

- Confirm `os` and `io` are absent.
- Confirm `dofile` and `loadfile` are absent.
- Confirm a read outside the module root fails.
- Confirm raw CLR access is absent.

`BudgetTests.cs`:

- Confirm the instruction hook stops an infinite loop.
- Confirm the host logs the stop and continues.
- Confirm the deadline stops a long host call.

`FaultTests.cs`:

- Confirm a compile error disables one script and not the module.
- Confirm a runtime error logs the file and the line.
- Confirm a thrown host binding does not stop the world.

`DeterminismTests.cs`:

- Confirm one script and one seed give the same state across two runs.
- Confirm the ordered iteration helper is stable.

`BindingTests.cs`:

- Round-trip each read binding.
- Confirm the interaction binding replaces the resolution.
- Confirm a command binding registers.

## 15. Docs pass

The change touches a config key, a format, and API surface. The docs pass is
mandatory.

- Add `docs/specs/scripting.md`. One subject: the host, the sandbox, the
  budgets, the bindings, the determinism rules, and the mod workflow.
- Add the new subject to `docs/README.md`.
- Update `docs/specs/config-schemas.md`. Record the rename and the new keys.
- Update `docs/architecture.md`. Add the `Swordfish.Scripting` module row.
- Update `docs/specs/composition.md`. Record the scripting composition entry
  and the binding registry.
- Update `README.md`. The scripting roadmap rows name C# scripting
  (`README.md:128-132`). Decide the new wording for the Lua path. Record the
  `Swordfish.Compilation` note at `README.md:31`.
- Update `AGENTS.md`. Add the `Swordfish.Scripting` repository index row.
- Refresh every `file:line` pointer that the change moves.

## 16. Risks

| Risk | Mitigation |
|---|---|
| The interpreter is not an OS boundary. | Capability sandbox and budgets now. Keep the process-isolation path open. State the limit in the doc. |
| Lua 5.2 doubles and undefined table order break determinism. | Keep v1 out of the predicted step. Provide a seeded RNG and ordered iteration. Add a determinism test before any sim scripting. |
| `LuaState` is not thread-safe. | Enforce thread affinity. One state per world. Never call Lua from two threads. |
| Boundary marshalling faults, such as nil and type errors. | Use `[LuaObject]` typed wrappers. Add round-trip tests. |
| Script state is lost on reload. | Declare the state ephemeral. Rebuild from `on_load`. |
| The runtime library is newer with a smaller community. | The library is MIT and pure C#. Keep the host behind an interface. MoonSharp, NLua, and Jint stay available. |
| A memory limit is not enforceable. | Treat the memory key as advisory. Record the limit. Use the process layer for hostile input. |
| A binding leaks a live object and breaks the sandbox. | Keep bindings read-only in v1. Review each binding. Add a sandbox test. |
| The config rename breaks existing files. | The project is pre-release. Update the schema and every `modules.toml` in one change. |
| Scripts join game saves. | Do not persist script state. Document that scripts rebuild on load. |

## 17. Future work

- An out-of-process script worker for the hostile tier.
- ECS writes through a queued command buffer.
- LuaLS `---@class` annotation stubs for editor IntelliSense.
- A script reload without a world restart.
- A deterministic scripting mode for the shared step, with tests.
- Binding parity tests across the client and the server.
- Profile counters for script time in the F3 overlay.
- A shim that records the "scripting" roadmap rows against the Lua path.

## 18. Source references

- Loader stub: `Shoal/Modularity/ModulesLoader.cs:80-90`, `:93`, `:106`,
  `:131-135`.
- Manifest script path: `Shoal/Modularity/ModuleManifest.cs:31-32`.
- Script gate: `Shoal/Modularity/ModuleOptions.cs:6-7`.
- Config schema: `docs/specs/config-schemas.md:22,44,49,55`.
- Stated intent: `README.md:22`, `:31`, `:128-132`.
- Compilation framework: `Swordfish.Compilation/Lexing/`, `Parsing/`, `Linting/`.
- DI scan and hook: `Shoal/AppEngine.cs:227-341`.
- Injection contract: `Shoal/DependencyInjection/IDryIocInjector.cs`.
- Composition rules: `docs/specs/composition.md`.
- Determinism and prediction: `docs/specs/networking-prediction.md:8`.
- Interaction hook: `WaywardBeyond.Gameplay/Interactions/IInteractionHandler.cs`,
  `InteractionHandlerRegistry.cs:17-37`.
- Command API: `Swordfish.Library/IO/Command.cs`.
- Client world tick: `Swordfish/ECS/ECSContext.cs:21-37`, `:67-71`.
- Server world tick: `WaywardBeyond.Server/ServerWorld.cs:48-57`,
  `ServerComposition.cs:29-30, 92-113, 120-128`.
- Server system marker: `WaywardBeyond.Server/IServerWorldSystem.cs`.
- ECS dirty rules: `docs/development.md:168-183`.
- Config convention: `docs/development.md:235-243`.

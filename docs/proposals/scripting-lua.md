# Lua Scripting — Proposal and Development Plan

Status: proposed

Scope: the engine tier (passive script discovery in `Shoal` and a new
`Swordfish.Scripting` library) and the game tier (`WaywardBeyond.Server`
multi-world host, events, chat feedback, and interaction pipeline hooks).
Engine changes commit separately from game changes. Client scripting is
architected in the discovery seam and deferred to a later change.

## 1. Summary

The engine has no scripting runtime. A loader stub exists, and it fails at
runtime. The documented intent is C# scripting. Compiled C# runs with full
process trust, so it cannot safely run community mods without exposing the host
machine.

The threat model is operator-installed community mods. A malicious script must
not steal personal information from a player or operator, read host files, or
infect a machine. The proposal uses Lua through Lua-CSharp, a pure C#
interpreter with no native dependency and a strict capability sandbox.

The change has three parts:

1. Passive side-aware script discovery in `Shoal` (`IScriptCatalog`). `Shoal`
   takes no Lua dependency.
2. A regular engine library `Swordfish.Scripting` (like `Krill` and `Reef`, no
   `manifest.toml`) holding the host, per-module capability sandbox, event bus,
   and core read bindings. It runs headless without `EngineContainer`.
3. Server integration in `WaywardBeyond.Server` (per-world `IScriptHost`,
   `ServerScriptSystem`, `player.join`/`player.leave` events, `chat` feedback,
   and `Validate`/`React` interaction hooks).

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

- `README.md:24-25` — `Shoal` provides "Dependency Injection, containerization,
  C# scripting, and a standardized module system".
- `README.md:33-34` — `Swordfish.Compilation` "will eventually provide
  implementations for C# scripting used by Shoal and Swordfish".
- `README.md:131-135` — the scripting roadmap marks Parser, Lexer, and Linter as
  done and Compiler as open.

### 2.4 The compilation framework

`Swordfish.Compilation` is a generic lexer, parser, and linter framework. It has
`Lexer<T>`, `TokenParser<TToken, TType>`, and `ILinter<T>`. It defines no
grammar, no AST, no emitter, and no language. Its only consumer is the CLI
parser in `Shoal/CommandLine`. The roadmap boxes describe the framework, not a
language.

### 2.5 Module loading and DI boot order

`AppEngine` builds two containers in sequence (`Shoal/AppEngine.cs:60-76`,
`329-363`):

1. `CreateCoreContainer` registers `VirtualFileSystem` and `IModulesLoader`
   (`AppEngine.cs:266-267`) and validates the core container.
2. `CreateModulesContainer` calls `IModulesLoader.Load(...)`
   (`AppEngine.cs:335`), which loads prebuilt DLLs with `Assembly.LoadFrom`
   (`ModulesLoader.cs:106`) and mounts each module's `assets/` into the shared
   `VirtualFileSystem`.
3. `AppEngine` mounts `AppPaths.DataRoot` last when `MountInVfs` is true
   (`AppEngine.cs:342-349`).
4. `AppEngine` scans loaded assemblies and runs `RegisterDryIocModules`
   (`AppEngine.cs:351-357`), then registers `CommandParser` from the static
   `Command[]` set (`AppEngine.cs:359`).

Because `IDryIocInjector` implementations run after `ModulesLoader.Load`
returns, a Shoal module cannot inject a script runner into `ModulesLoader`.
`ModulesLoader` only discovers and records script entrypoints during `Load`.

### 2.6 Architecture constraints

- **Standalone ECS (`Krill`).** Core ECS types (`World`, `DataStore`, `Entity`,
  `Uuid`, `IEntitySystem`) live in `Krill/` (`net8.0`, zero repo dependencies).
  Engine components (`IdentifierComponent`, `TransformComponent`,
  `PhysicsComponent`) and `ECSContext` live in `Swordfish/ECS/`.
- **Headless server.** `WaywardBeyond.Server.Launcher/config/modules.toml` does
  not load `swordfish`, and the dedicated server does not run `EngineContainer`.
  Scripting must work without `Swordfish` or OpenGL.
- **Singleplayer dual-side process.** `WaywardBeyond.Client.Launcher/config/modules.toml`
  loads both `swordfish` and `waywardbeyond.server` in the same process. Script
  discovery must distinguish server, client, and shared scripts so singleplayer
  never double-executes a script.
- **Multi-world server.** `ServerWorldHost` creates a `ServerWorld` with an
  exclusive DryIoc child container on level join and disposes it on idle unload
  (`docs/specs/networking-worlds.md:9-28`). Per-world services register as
  transient templates and pin in `ServerWorld` (`ServerWorld.cs:41-61`). Shared
  singletons pre-resolve in `ServerComposition.PinSharedSingletons`
  (`ServerComposition.cs:126-134`).
- **5-stage interaction pipeline.** `IInteractionHandler` runs across
  `Select → Resolve → Validate → Commit → React` with priority bands, filters,
  mutable `ref InteractionContext`, and `InteractionDecision`
  (`docs/specs/interaction-pipeline.md`). `InteractionHandlerRegistry` is a
  shared stateless root singleton. `WaywardBeyond.Gameplay` has no `Injector`
  and does not reference DryIoc (`docs/specs/composition.md:59`).
- **Determinism.** `docs/specs/networking-prediction.md:8-15` states that the
  gameplay simulation is a shared, deterministic step. The client predicts and
  reconciles. A script that touches the simulation must agree on the client and
  the server.
- **No AOT.** No project sets `PublishAot` or `PublishTrimmed`. `Shoal`,
  `Krill`, and `Swordfish` target `net8.0`. The game targets `net9.0`.

## 3. Goals and non-goals

Goals:

- Ship a useful server-only v1: server world host, `player.join` and
  `player.leave` events, `Validate` and `React` interaction hooks, read-only
  ECS views, and `chat.send`/`chat.broadcast` player feedback.
- Architect side-aware discovery (`Server`, `Client`, `Shared`) so client
  scripting plugs in later without refactoring.
- Remove ambient host capabilities. A script cannot read host files, user data,
  environment variables, or execute OS commands or raw CLR code.
- Isolate each module in its own `LuaState` per world so one module cannot
  poison another module's globals, standard library, or metatables.
- Isolate faults. A broken script logs its file and line and does not crash the
  server or world.
- Keep scripting out of the predicted simulation step in v1.
- Keep `Shoal` free of any Lua dependency through passive script discovery.
- Let a module add scripts without a change to a core file.
- Document the trust model and the determinism rules.

Non-goals:

- No client script execution (`ClientScriptSystem`) in v1. Discovery supports
  `Scripts/Client/` so client execution can land cleanly later.
- No execution deadlines, instruction count hooks, or memory caps in v1. The
  trust model is operator-installed mods, and hardening targets security
  escape risks rather than performance or freeze DoS.
- No VM-swapping abstraction layer for Jint or MoonSharp. Binding projects use
  Lua-CSharp's `[LuaObject]` source generator directly.
- No scripting of the client-predicted physics step in v1.
- No ECS component writes from scripts in v1. Bindings are read-only.
- No `Commit` or `Resolve` interaction overrides in v1.
- No module config TOML binding in v1.
- No in-game command registration in v1. `CommandParser` is a static startup
  array (`Shoal/AppEngine.cs:359`) with no runtime dispatch wiring yet.
- No C# scripts. Compiled C# cannot be sandboxed.
- No visual scripting and no hot reload in v1.

## 4. Decisions

| Decision | Choice |
|---|---|
| v1 scope | Server-only useful v1 (`Validate`/`React` interactions, `player.join`/`player.leave`, `chat.send`/`chat.broadcast`, read-only ECS). Side-aware architecture ready for future client scripting. |
| Runtime | Lua 5.2 through Lua-CSharp (`LuaCSharp`, namespace `Lua`). Pure C#, MIT, no native dependency, AOT-safe. |
| Packaging | `Swordfish.Scripting` is a regular engine library (`net8.0`, like `Krill` and `Reef`), not a Shoal module. Wired via idempotent `container.RegisterScripting()`. |
| Trust model | Operator-installed mods. Strict capability sandbox against data theft and machine compromise. No performance deadlines or instruction-count caps. |
| Discovery seam | Passive `IScriptCatalog` in `Shoal`. `ModulesLoader` discovers `Scripts/Server/init.lua`, `Scripts/Client/init.lua`, and `Scripts/Shared/init.lua`, tagged with `ScriptSide`. Other files load via `require`. |
| Isolation unit | One `LuaState` per module per world, each with its own read-only `LuaPlatform` jailed to that module's `Scripts/` root. |
| Bindings coupling | Embrace Lua-CSharp directly (`[LuaObject]`). Keep `IScriptHost` at the world/DI/test boundary. |
| Thread model | Strict thread affinity. Server worlds run on `"Server"`. Synchronous `ValueTask` completion only. |
| Config | Rename `AllowScriptCompilation` to `AllowScripting` in `modules.toml`. No `scripting.toml` needed in v1. |

## 5. Trust model

The trust model is **operator-installed community mods**, not untrusted
runtime-submitted scripts. The security threat is a malicious mod or a
compromised server script stealing personal information (user profiles, saves,
host files, environment variables) or infecting a machine (shell commands,
arbitrary disk writes, raw CLR execution). Freezing or crashing the game is
not a threat in this model because the operator chooses which mods to install.

The proposal enforces two layers:

1. **Strict capability sandbox (v1).** A script can call only the host APIs
   that the host explicitly exposes. The sandbox denies `os`, `io`, `debug`,
   `package`, `dofile`, `loadfile`, `load`, `loadstring`, raw CLR access,
   reflection, environment variables, shell execution, and network sockets.
   Each module runs in its own `LuaState` with a read-only `LuaPlatform` jailed
   to that module's `Scripts/` directory. Every exposed binding is audited
   before shipping to ensure it cannot leak host paths, `AppPaths.DataRoot`,
   `profile.toml`, saves, player IPs, or secrets.
2. **Fault isolation (v1).** Every entry into Lua runs inside `try`/`catch`.
   Compile errors and runtime errors log the module, file, and line and do not
   crash the server or world.

Lua-CSharp is an in-process interpreter, not an operating-system boundary. The
documentation must state this limit. Because v1 targets security rather than
CPU/memory DoS, v1 intentionally omits `SetHook` instruction budgets,
wall-clock deadlines, and memory caps, avoiding per-instruction hook overhead
and false-positive kills on heavy mods.

## 6. Host architecture

### 6.1 Projects

- **`Shoal`.** Gains `ScriptSide` (`Server`, `Client`, `Shared`),
  `DiscoveredScript` (module id, module scripts root path, entrypoint path,
  side), and `IScriptCatalog`. `Shoal` does not reference Lua.
- **`Swordfish.Scripting` (new regular library).** Target `net8.0`. No
  `manifest.toml`. References `Shoal`, `Krill`, `Swordfish.Library`,
  `Swordfish` (or shared component types if split), and `LuaCSharp`. Owns
  `IScriptHost`, `LuaScriptHost`, `LuaModuleRuntime`, `LuaSandbox`,
  `ScriptEventBus`, and core read bindings (`log`, `time`, `random`, `ecs`,
  `events`). Composition entry `ScriptingComposition.RegisterScripting(this IContainer)`
  using `IfAlreadyRegistered.Keep` so any embedding can call it idempotently.
- **`WaywardBeyond.Server`.** References `Swordfish.Scripting`, calls
  `container.RegisterScripting()`, pins `IScriptCatalog` in
  `PinSharedSingletons`, pins a per-world `IScriptHost` in `ServerWorld`,
  registers `ServerScriptSystem : IServerWorldSystem`, wires
  `player.join`/`player.leave` and `chat` bindings, and registers the
  `Validate`/`React` interaction pipeline handlers.

### 6.2 Discovery seam in `Shoal`

`ModulesLoader.LoadModule` does not execute scripts. During `Load`:

1. Compute the scripts root path. Keep the default `<module>/Scripts/`.
2. If the directory does not exist, continue.
3. If `AllowScripting` is false, log once and skip.
4. If `AllowScripting` is true, check for the three side entrypoints in
   deterministic order:
   - `Scripts/Shared/init.lua` (`ScriptSide.Shared`)
   - `Scripts/Server/init.lua` (`ScriptSide.Server`)
   - `Scripts/Client/init.lua` (`ScriptSide.Client`)
5. Record each existing entrypoint in `IScriptCatalog` in module load order.

Other `.lua` files inside `Scripts/Server/`, `Scripts/Client/`, and
`Scripts/Shared/` are not auto-executed. Entrypoints load them via `require`,
which prevents double-execution of helper modules and gives modders explicit
load order. A module with scripts and no assemblies already works because
`ModulesLoader` returns early at `ModulesLoader.cs:93-96` after recording the
entrypoints.

### 6.3 DI and composition

- **Core container (`Shoal/AppEngine.cs`):** Registers `IScriptCatalog`
  (`ScriptCatalog`, `Reuse.Singleton`). `ModulesLoader` populates it during
  `Load`.
- **Server (`WaywardBeyond.Server/ServerComposition.cs`):**
  - Calls `container.RegisterScripting()`.
  - Adds `IScriptCatalog` and `ServerWorldScriptRegistry` to
    `PinSharedSingletons` (`ServerComposition.cs:126-134`).
  - Registers `IScriptHost` as a disposable transient template
    (`allowDisposableTransient: true`) configured for `ScriptSide.Server`
    (which loads `Shared` then `Server` entrypoints).
  - `ServerWorld` resolves and pins its `IScriptHost`, registers
    `(Store, ScriptHost)` in `ServerWorldScriptRegistry`, and removes and
    disposes it in `ServerWorld.Dispose()` (`ServerWorld.cs:41-73`).
- **Future client (`Swordfish/EngineContainer.cs`):** Will call
  `container.RegisterScripting()` and create a client `IScriptHost` configured
  for `ScriptSide.Client` (`Shared` then `Client` entrypoints). No refactoring
  of `Shoal` or `Swordfish.Scripting` is required.

### 6.4 Lifecycle

- **Startup:** `ModulesLoader` populates `IScriptCatalog` with discovered
  entrypoints.
- **World creation:** When `ServerWorld` constructs its `IScriptHost`, the host
  creates one `LuaState` per module that has `Shared` or `Server` entrypoints,
  installs the sandbox and bindings, runs `Shared/init.lua` (if present) and
  then `Server/init.lua` (if present), and caches whether the module defined
  `on_load`, `on_tick`, `on_event`, or `on_interaction`.
- **Load hook:** The host runs each module's `on_load()` once on the `"Server"`
  thread before the first tick.
- **Tick hook:** Each tick, `ServerScriptSystem` invokes `on_tick(dt)` only for
  modules that defined it (zero Lua lookup for modules without `on_tick`).
- **Teardown:** `ServerWorld.Dispose()` unregisters the world from
  `ServerWorldScriptRegistry` and disposes `IScriptHost`, dropping every
  module's `LuaState`.
- **Ephemeral state:** Script state is ephemeral. A recreated server world
  rebuilds script state from `on_load()`. Saves do not carry script state.

### 6.5 Threading and synchronous execution

Lua-CSharp states that `LuaState` is not thread-safe. The host enforces strict
thread affinity:

- **Server.** Every `LuaState` owned by a `ServerWorld` runs only on the
  `"Server"` thread (`WaywardBeyond.Server/ServerWorld.cs:64-67`,
  `docs/specs/networking-worlds.md:120-123`).
- A host binding never calls into Lua from a foreign thread.

Lua-CSharp executes through `ValueTask`-returning methods (`DoStringAsync`,
`CallAsync`). Because `World.Tick` and `IInteractionHandler.Handle` are
synchronous hot paths, v1 forbids async-yielding host functions inside script
callbacks. The host invokes the `ValueTask`, asserts `IsCompleted`, and logs
and disables any module that attempts to suspend asynchronously. This avoids
per-tick `Task` allocations and thread-pool hops.

## 7. Sandbox and fault isolation

### 7.1 Per-module `LuaState` and jailed `LuaPlatform`

Each module in a world owns its own `LuaState` and its own read-only
`LuaPlatform`. Never pass the app-wide `VirtualFileSystem` from DI, because
`AppEngine` mounts every module's `assets/` and `AppPaths.DataRoot` (user
settings and saves) into it (`Shoal/AppEngine.cs:338, 342-349`).

Instead, each module's `LuaPlatform` wraps a path-jailed read-only file loader
restricted to that module's permitted `Scripts/` subdirectories (`Shared/` plus
the active side's folder). It rejects `..`, rooted paths, and symlinks escaping
the root. `print` routes to `ILogger`. The platform denies the OS environment,
standard I/O, and shell access.

### 7.2 Denied surface

The host does not open the full standard library. It opens only a curated
subset (`base` subset, `math` with replaced RNG, `string`, `table`) plus a
module-jailed `require` function. It denies:

- `os` and `io`.
- `debug`.
- `package` (raw loaders, `package.loadlib`, `package.cpath`).
- `dofile` and `loadfile`.
- `load` and `loadstring` for dynamic code execution from strings.
- Raw CLR access, reflection, and arbitrary network or disk APIs.

Because each module has its own `LuaState`, one module cannot mutate `string`,
`table`, or string metatables for any other module in the world.

### 7.3 Fault isolation

The host wraps every entry into Lua in `try`/`catch`. It catches
`LuaCompileException`, `LuaRuntimeException`, and host binding exceptions. It
logs the module id, message, file, and line. A module whose entrypoint fails to
compile is disabled for that world with a log entry. A runtime fault in one
module does not affect other modules or stop the world.

## 8. Determinism rules

Lua-CSharp implements Lua 5.2. Lua 5.2 has no integer subtype. All numbers are
doubles. Table iteration order is undefined. Both are determinism hazards.

The proposal scopes the hazard away:

- **v1 runs no script in the predicted physics step.** Scripts run only as
  server-authoritative hooks. They do not take part in client prediction
  (`docs/specs/networking-prediction.md:8-15`).
- **Each module gets its own seeded RNG.** The sandbox replaces `math.random`
  with a per-module host RNG seeded from `(worldSeed, moduleId)` so module load
  order does not perturb another module's random sequence.
- **The host provides an ordered iteration helper.** The documentation records
  undefined `pairs` order and provides a sorted iteration helper.
- **Scripts do not read the wall clock.** The host provides the world sim tick
  and fixed delta.

A later change that scripts the shared step must add a determinism test to
`Swordfish.Tests` first. The test must prove identical state across two runs.

## 9. Script model and bindings

### 9.1 Script callbacks

A module's entrypoint (`init.lua`) defines optional global functions in its own
`LuaState`:

| Function | When | Scope |
|---|---|---|
| `on_load()` | Once when the world host loads | Server (client later) |
| `on_tick(dt)` | Each world tick (skipped if undefined) | Server (client later) |
| `on_event(name, payload)` | On a named world event | Server (client later) |
| `on_interaction(ctx)` | On a matching server interaction stage | Server only |

### 9.2 v1 bindings

Bindings use Lua-CSharp's `[LuaObject]` source generator directly for
zero-wrapper, compile-time marshalling. Every binding is audited against data
exfiltration and sandbox escape.

Core engine bindings (`Swordfish.Scripting`):

- **Log.** `log.info(msg)`, `log.warn(msg)`, `log.error(msg)` tagged with the
  module id.
- **Time.** `time.tick()` (world sim tick) and `time.delta()` (fixed delta).
- **Random.** Per-module seeded RNG replacing `math.random`.
- **ECS read views.** `ecs` read-only lookup over `Krill`'s `DataStore` by
  `Uuid`, exposing curated read-only snapshots of `IdentifierComponent`,
  `TransformComponent`, and `PhysicsComponent`. Never exposes raw `DataStore`
  handles.
- **Events.** `events.publish(name, payload)` and `on_event(name, payload)` via
  a per-world `ScriptEventBus`.

Server gameplay bindings (`WaywardBeyond.Server`):

- **Core server events.** `ServerJoinSystem` publishes `player.join` and
  `player.leave` (with player `Uuid` and display name) into the world's
  `ScriptEventBus`.
- **Chat feedback.** `chat.send(playerUuid, text)` and `chat.broadcast(text)`
  using the existing server chat relay (`docs/specs/chat.md`) so scripts can
  explain cancelled interactions or greet joining players.
- **Interaction hooks.** `Validate` and `React` pipeline bridge (see §9.3).

Deferred to v2:

- **ECS writes** through a queued command buffer following
  `docs/development.md:343-361`.
- **Module config TOML binding** once a per-module script config format exists.
- **In-game commands** once `CommandParser` (`Swordfish.Library/IO/CommandParser.cs`)
  supports dynamic registration and in-game dispatch.
- **`Commit` and `Resolve` interaction overrides** with controlled write APIs.

### 9.3 Interaction pipeline hook

`IInteractionHandler` (`WaywardBeyond.Gameplay/Interactions/IInteractionHandler.cs`)
runs a 5-stage pipeline (`Select → Resolve → Validate → Commit → React`) with
priority bands and `InteractionDecision` (`Continue`, `Handled`, `Consume`,
`Cancel`). See `docs/specs/interaction-pipeline.md`.

`InteractionHandlerRegistry` is a shared stateless root singleton
(`ServerWorld.cs:49-50`), while `IScriptHost` is per world. The bridge works as
follows:

- `WaywardBeyond.Server` registers a root singleton `ServerWorldScriptRegistry`
  (pinned in `PinSharedSingletons`) mapping `DataStore` to `IScriptHost`.
  `ServerWorld` registers on construction and removes in `Dispose()`.
- `WaywardBeyond.Server` registers stateless `ScriptValidateInteractionHandler`
  (stage `Validate`, priority `Normal`) and `ScriptReactInteractionHandler`
  (stage `React`, priority `Normal`) as `IInteractionHandler` implementations.
- On each interaction, the handler looks up `context.Store` in
  `ServerWorldScriptRegistry`. If no module in that world defined
  `on_interaction`, it returns `InteractionDecision.Continue` immediately with
  zero allocations.
- Otherwise, it passes a read-only `[LuaObject]` snapshot of the interaction
  (stage name, player `Uuid`, interaction kind, target voxel, held item id,
  resolved action) to each hooked module.
- In `Validate`, returning `"cancel"` stops the pipeline with
  `InteractionDecision.Cancel` before `Commit`. Returning `"continue"` or `nil`
  lets the pipeline proceed.
- In `React`, the script observes the committed outcome and returns nothing.

This reuses the existing pipeline with no new wire message and keeps
`WaywardBeyond.Gameplay` free of DryIoc and Lua dependencies.

## 10. Config and manifest

### 10.1 `modules.toml`

Rename the gate in `Shoal/Modularity/ModuleOptions.cs` and update
`docs/specs/config-schemas.md:47-60` plus all three launcher `modules.toml`
files in the same change:

| Key | Type | Purpose | Default |
|---|---|---|---|
| `AllowScripting` | bool | enable Lua script discovery and execution | `false` |
| `LoadOrder` | string[] | module ids in load order | `[]` |

The old key `AllowScriptCompilation` is removed. The project is pre-release, so
this is a clean break. No `scripting.toml` is required in v1.

### 10.2 `manifest.toml`

`ScriptsPath` stays (`Shoal/Modularity/ModuleManifest.cs:31-32`). Its default
stays `<module>/Scripts/`. No new manifest key is required. A manifest with
scripts and no assemblies is valid.

## 11. Integration

### 11.1 Server

`WaywardBeyond.Server/ServerComposition.cs` calls `RegisterScripting()`, pins
`IScriptCatalog` and `ServerWorldScriptRegistry` in `PinSharedSingletons`, and
registers `IScriptHost` as a disposable per-world transient template.
`ServerWorld` pins `IScriptHost`, registers it in `ServerWorldScriptRegistry`,
and removes and disposes it on world teardown.

Add `ServerScriptSystem : IServerWorldSystem` and register it through
`RegisterServerSystem<T>` (`ServerComposition.cs:141-144`). Place it after
`ServerChatSystem` and before `NetworkPublishSystem` in the canonical tick
order (`ServerComposition.cs:86-104`):

```
... → ServerInteractionSystem → ServerChatSystem → ServerScriptSystem → NetworkPublishSystem
```

### 11.2 Future client integration

When a client scripting use case arrives, `Swordfish/EngineContainer.cs` calls
`RegisterScripting()` (idempotent via `Keep`), creates one client `IScriptHost`
filtered to `ScriptSide.Client` (`Shared` + `Client` entrypoints), and adds
`ClientScriptSystem : IEntitySystem` on the `"ECS"` thread. `Shoal` and
`Swordfish.Scripting` need no structural changes.

### 11.3 Failure isolation

A script fault must not stop a world. `ServerScriptSystem` and the interaction
handlers catch every fault, log the module, file, and line, and continue. This
matches the fault isolation pattern in `docs/specs/networking-rpc.md`.

## 12. Modding workflow

1. Add a `Scripts/Server/` directory to the module (exact casing for Linux).
2. Add `Scripts/Server/init.lua` (and any helper files loaded via `require`):

```lua
-- Scripts/Server/init.lua
local players = 0

function on_load()
    log.info("greeter loaded")
end

function on_event(name, payload)
    if name == "player.join" then
        players = players + 1
        chat.send(payload.player_id, "Welcome! Players joined: " .. players)
    end
end

function on_interaction(ctx)
    if ctx.stage == "validate" and ctx.action == "break" and ctx.y < 0 then
        chat.send(ctx.player_id, "Protected zone: breaking is disabled here.")
        return "cancel"
    end
    return "continue"
end
```

3. Enable scripting in `modules.toml`:

```toml
AllowScripting = true
LoadOrder = [
    "waywardbeyond.bricks",
    "waywardbeyond.items",
    "waywardbeyond.skills",
    "waywardbeyond.bodies",
    "waywardbeyond.server",
    "my.module",
]
```

4. The module edits no core file. The module does not touch the loader, the
   composition, or a transport serializer.

## 13. Development plan

Each phase builds green and has its own commit. Phases 1 and 2 are engine tier
and commit first, standalone-green. Phase 3 is game tier. Phase 4 is the docs
pass.

### Phase 1 — `Shoal` discovery seam and `Swordfish.Scripting` core (engine)

New files:

- `Shoal/Modularity/ScriptSide.cs`
- `Shoal/Modularity/DiscoveredScript.cs`
- `Shoal/Modularity/IScriptCatalog.cs`
- `Shoal/Modularity/ScriptCatalog.cs`
- `Swordfish.Scripting/Swordfish.Scripting.csproj`
- `Swordfish.Scripting/ScriptingComposition.cs`
- `Swordfish.Scripting/IScriptHost.cs`
- `Swordfish.Scripting/LuaScriptHost.cs`
- `Swordfish.Scripting/LuaModuleRuntime.cs`
- `Swordfish.Scripting/LuaSandbox.cs`
- `Swordfish.Scripting/JailedScriptFileSystem.cs`

Changed files:

- `Shoal/Modularity/ModulesLoader.cs` discovers `Shared`, `Server`, and `Client`
  `init.lua` entrypoints into `IScriptCatalog` when `AllowScripting` is true.
- `Shoal/Modularity/ModuleOptions.cs` renames `AllowScriptCompilation` to
  `AllowScripting`.
- `Shoal/AppEngine.cs` registers `IScriptCatalog` in the core container.
- Launcher `modules.toml` files update the renamed key.

Verify `LuaCSharp` and its source generator build cleanly on `net8.0` alongside
`Krill.Generator`, `Reef.Generator`, and `Needlefish.Generators`, and bundle
into both client and server launchers.

Tests (`Swordfish.Tests/Scripting/`): side discovery (`Shared`/`Server`/`Client`),
per-module `LuaState` isolation, sandbox denial, jailed `require` path
traversal rejection, sync `ValueTask` guard, fault isolation.

### Phase 2 — events and core read bindings (engine)

New files:

- `Swordfish.Scripting/Events/ScriptEventBus.cs`
- `Swordfish.Scripting/Bindings/LogBinding.cs`
- `Swordfish.Scripting/Bindings/TimeBinding.cs`
- `Swordfish.Scripting/Bindings/RandomBinding.cs`
- `Swordfish.Scripting/Bindings/EcsReadBinding.cs`

Tests: event delivery, per-module RNG seed independence, ordered iteration
helper, ECS read views (`IdentifierComponent`, `TransformComponent`,
`PhysicsComponent`), zero-lookup skip when `on_tick` is undefined.

### Phase 3 — multi-world server host, events, chat, and interactions (game)

New files:

- `WaywardBeyond.Server/Scripting/ServerWorldScriptRegistry.cs`
- `WaywardBeyond.Server/Scripting/ChatScriptBinding.cs`
- `WaywardBeyond.Server/Systems/ServerScriptSystem.cs`
- `WaywardBeyond.Server/Interactions/ScriptValidateInteractionHandler.cs`
- `WaywardBeyond.Server/Interactions/ScriptReactInteractionHandler.cs`

Changed files:

- `WaywardBeyond.Server/WaywardBeyond.Server.csproj` references
  `Swordfish.Scripting`.
- `WaywardBeyond.Server/ServerComposition.cs` calls `RegisterScripting()`,
  pins `IScriptCatalog` and `ServerWorldScriptRegistry`, registers
  `ServerScriptSystem` before `NetworkPublishSystem`, and registers the
  interaction handlers.
- `WaywardBeyond.Server/ServerWorld.cs` pins per-world `IScriptHost`, registers
  it in `ServerWorldScriptRegistry`, and removes and disposes it on teardown.
- `WaywardBeyond.Server/Systems/ServerJoinSystem.cs` publishes `player.join`
  and `player.leave` to the world's `ScriptEventBus`.

Tests: per-world `LuaState` isolation, world unload cleanup (no `DataStore`
leak), `player.join`/`player.leave` delivery, `chat.send`/`chat.broadcast`,
`Validate` cancel decision, `React` observation, zero-alloc interaction
short-circuit when no scripts hook `on_interaction`, headless server startup
without `Swordfish`.

### Phase 4 — docs pass

- Add `docs/specs/scripting.md`.
- Update `docs/specs/config-schemas.md` for `AllowScripting`.
- Update `docs/README.md`, `docs/architecture.md`, `docs/specs/composition.md`,
  `docs/specs/networking-worlds.md`, and `docs/specs/interaction-pipeline.md`.
- Update `README.md` and `AGENTS.md`.

## 14. Test plan

Tests live in `Swordfish.Tests/Scripting/` (`net9.0`).

`DiscoveryTests.cs`:

- Confirm `Shared/init.lua`, `Server/init.lua`, and `Client/init.lua` are
  discovered with the right `ScriptSide` and in module load order.
- Confirm helper `.lua` files are not auto-executed as entrypoints.
- Confirm `AllowScripting = false` skips discovery.

`SandboxTests.cs`:

- Confirm `os`, `io`, `debug`, `package`, `dofile`, `loadfile`, `load`, and
  `loadstring` are absent.
- Confirm `require` inside the module's permitted folders succeeds, and path
  traversal (`..`), rooted paths, and cross-side reads fail.
- Confirm raw CLR access is absent.
- Confirm mutating globals or `string` metatables in module A does not affect
  module B in the same world.

`FaultTests.cs`:

- Confirm a compile error disables one module and not others.
- Confirm a runtime error logs module, file, and line and does not stop the
  world.
- Confirm an async-yielding host call fails cleanly instead of hanging the
  tick.

`DeterminismTests.cs`:

- Confirm one module and one world seed produce the same RNG sequence across
  two runs, independent of other modules' load order.
- Confirm the ordered iteration helper is stable.

`ServerIntegrationTests.cs`:

- Round-trip `log`, `time`, `random`, `ecs` read views, `events`, and `chat`.
- Confirm two `ServerWorld` instances own independent `LuaState` instances and
  disposing one unregisters it from `ServerWorldScriptRegistry`.
- Confirm `ScriptValidateInteractionHandler` can cancel an interaction and
  `ScriptReactInteractionHandler` observes committed outcomes.

## 15. Docs pass

The change touches a config key, a module folder layout, and public API
surface. The docs pass is mandatory:

- Add `docs/specs/scripting.md`. One subject: discovery, host, sandbox,
  bindings, determinism rules, and mod workflow.
- Add the new subject to `docs/README.md`.
- Update `docs/specs/config-schemas.md` for `AllowScripting`.
- Update `docs/architecture.md` with the `Swordfish.Scripting` row.
- Update `docs/specs/composition.md` with `RegisterScripting()`,
  `IScriptCatalog`, and `PinSharedSingletons`.
- Update `docs/specs/networking-worlds.md` with `IScriptHost` in the per-world
  list and `ServerScriptSystem` in the tick order.
- Update `docs/specs/interaction-pipeline.md` with the script `Validate` and
  `React` handlers.
- Update `README.md` (`:24-25`, `:33-34`, `:131-135`) and `AGENTS.md`.
- Refresh every moved `file:line` pointer.

## 16. Risks

| Risk | Mitigation |
|---|---|
| The interpreter is not an OS boundary. | Strict capability sandbox and per-module `LuaState` isolation. Audit every exposed binding for data-exfiltration or escape paths. State the limit in the doc. |
| A binding leaks a live host object or sensitive data. | Keep bindings read-only in v1 (except `chat` relay). Never expose `DataStore`, `IContainer`, `VirtualFileSystem`, host paths, or player secrets. Add sandbox tests. |
| Lua 5.2 doubles and undefined table order break determinism. | Keep v1 out of the predicted step. Provide per-module seeded RNG and ordered iteration. Add a determinism test before any sim scripting. |
| `LuaState` is not thread-safe. | Enforce thread affinity. Run server scripts only on `"Server"`. Never call Lua from a foreign thread. |
| Async `ValueTask` calls block or allocate on tick threads. | Forbid async-yielding host calls in tick and interaction hooks. Assert synchronous completion. |
| `ServerWorldScriptRegistry` leaks a disposed `DataStore`. | Register in `ServerWorld` constructor and remove in `ServerWorld.Dispose()`. Pin with a disposal test. |
| Script state is lost on world unload or reload. | Declare the state ephemeral. Rebuild from `on_load()`. |
| The config rename breaks existing files. | The project is pre-release. Update the schema and all three `modules.toml` files in one change. |

## 17. Future work

- Client script execution (`ClientScriptSystem` on `"ECS"` thread loading
  `Scripts/Shared/init.lua` and `Scripts/Client/init.lua`).
- ECS writes through a queued command buffer.
- Per-module script TOML configuration binding.
- Dynamic `CommandParser` registration and `CommandBinding` once an in-game
  command dispatch path exists.
- `Commit` and `Resolve` interaction overrides with controlled write APIs.
- LuaLS `---@class` annotation stubs for editor IntelliSense.
- Script reload without a world restart.
- A deterministic scripting mode for the shared step, with tests.
- Profile counters for script time in the F3 overlay.

## 18. Source references

- Loader stub: `Shoal/Modularity/ModulesLoader.cs:80-90`, `:93-96`, `:106`,
  `:132-135`.
- Manifest script path: `Shoal/Modularity/ModuleManifest.cs:31-32`.
- Script gate: `Shoal/Modularity/ModuleOptions.cs:6-7`.
- DI boot order and VFS mounting: `Shoal/AppEngine.cs:60-76`, `:266-267`,
  `:329-363`.
- Injection contract: `Shoal/DependencyInjection/IDryIocInjector.cs`.
- Virtual file system: `Swordfish.Library/IO/VirtualFileSystem.cs`.
- Command API and parser: `Swordfish.Library/IO/Command.cs`,
  `Swordfish.Library/IO/CommandParser.cs:10-20`.
- Config schema: `docs/specs/config-schemas.md:22,44,49,55,283-309`.
- Stated intent: `README.md:24-25`, `:33-34`, `:131-135`.
- Compilation framework: `Swordfish.Compilation/Lexing/`, `Parsing/`, `Linting/`.
- Standalone ECS: `docs/specs/krill.md`.
- Composition rules: `docs/specs/composition.md:45-96`.
- Determinism and prediction: `docs/specs/networking-prediction.md:8-22`.
- Interaction pipeline: `docs/specs/interaction-pipeline.md`,
  `WaywardBeyond.Gameplay/Interactions/IInteractionHandler.cs`,
  `WaywardBeyond.Gameplay/Interactions/InteractionHandlerRegistry.cs`.
- Chat relay: `docs/specs/chat.md`.
- Client world tick: `Swordfish/ECS/ECSContext.cs:22-38`, `:68-72`.
- Client composition: `Swordfish/EngineContainer.cs`.
- Server world lifecycle and tick: `WaywardBeyond.Server/ServerWorld.cs:41-73`,
  `WaywardBeyond.Server/ServerComposition.cs:68-144`,
  `docs/specs/networking-worlds.md`.
- Server system marker: `WaywardBeyond.Server/IServerWorldSystem.cs`.
- RPC fault handling: `docs/specs/networking-rpc.md`.
- ECS dirty rules: `docs/development.md:343-361`.

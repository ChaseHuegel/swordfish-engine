# DI Composition

One subject: who registers what in the game's dependency-injection setup.
It records the ownership rules, the call sites, the registration policies, and
the order contracts. The code is always the authority.

## Model

`AppEngine` builds one module container per embedding. Each Shoal module adds
its registrations through its `IDryIocInjector`. The server creates a child
container per world. See [networking-worlds](networking-worlds.md).

Every project owns its own registrations. A project exposes one composition
entry point next to its API types. Embedding compositions call the entry point
instead of enumerating the project's types. This prevents drift: client, host,
and dedicated server cannot fall out of sync because they all run the same
call. Entries are extension methods on `IContainer`, named
`RegisterXxx(this IContainer)`. See [naming](naming.md) for the style.

## Call sites

The server module is the single host composition call site. Both embeddings
load it, so the wire-up cannot drift:

`WaywardBeyond.Server/ServerModule.cs` calls:
- `container.RegisterHost()` — the shared host wire-up.
- `container.RegisterServer()` — the authoritative server composition.

`container.RegisterHost()` (`WaywardBeyond.Server/HostComposition.cs`) calls:
- `container.RegisterNetworking()` — the wire catalogue.
- `container.RegisterNetworkingConfig()` — the networking config.

`container.RegisterServer()` (`WaywardBeyond.Server/ServerComposition.cs`)
calls `container.RegisterDataServices()` and
`container.RegisterPermissions()`.

The client module (`WaywardBeyond.Client/Injector.cs`) registers client-only
services and the embedding's content choices. It calls
`container.RegisterDataServices()` and `container.RegisterTags()` because menu
pages use `SeedPhraseService` and `LoadScreen` and `NameGenerator` read
localized tags. The dedicated launcher registers no services. It applies CLI
overrides after the engine builds
(`WaywardBeyond.Server.Launcher/Program.cs`).

## Project-owned composition entries

| Project | Entry point | Owns |
|---|---|---|
| `WaywardBeyond.Networking` | `RegisterNetworking()` | `NetworkRegistry` and the built-in wire components |
| `WaywardBeyond.Config` | `RegisterNetworkingConfig()` | `network.toml` config, its sub-config views, and validation |
| `WaywardBeyond.Data` | `RegisterDataServices()` | shared data services (`Migrator`, `SeedPhraseService`, `ISlotGroupSchemaDatabase` / `SlotGroupSchemaDatabase`), bundled blueprints, tags, and the content pack version entry point |
| `WaywardBeyond.Data` (blueprints) | `BlueprintComposition.RegisterBundledBlueprints()` | headless bundled `.wbp` database (`IBundledBlueprintDatabase`) and parser (`BlueprintDataParser`) |
| `WaywardBeyond.Data` (localization) | `LocalizationComposition.RegisterTags()` | localized tag database and parser |
| `WaywardBeyond.Server` | `RegisterHost()`, `RegisterServer()` | host wire-up, server composition |
| `WaywardBeyond.Bricks` | `Injector` | brick and paint databases, registries, parsers |
| `WaywardBeyond.Items` | `Injector` | item database and parser |
| `WaywardBeyond.Recipes` | `Injector` | recipe database, parser, tags |
| `WaywardBeyond.Bodies` | `Injector` | body database, parser |
| `WaywardBeyond.Skills` | `Injector` | skill database, parser, tags |
| `WaywardBeyond.Permissions` | `RegisterPermissions()` | permission file parser, loader, and compiled policy |
| `WaywardBeyond.Gameplay` (statistics) | `StatisticsComposition.RegisterStatistics()`, `RegisterStatModifierSource<T>()` | built-in `IStatModifierSource` implementations, `IStatResolver`, and idempotent modifier source registration for modules and mods. `ServerComposition` still owns `IInteractionHandlerRegistry` and the default interaction handlers |

## Embedding choices

An embedding makes its choices through overrides, never by re-registering
shared wire-up:

- Singleplayer loopback: `ClientRpcHost` connects the shared
  `LoopbackRpcServerTransport` at startup. The dedicated server does not create
  a client host.
- RPC hosts: `HostComposition` registers the loopback transport.
  `ServerComposition` registers `ServerPeers`, `ServerHostCommands`,
  `ServerHandlers`, and `ServerRpcHost`. The client module registers
  `ClientInbound`, `ClientHandlers`, and `ClientRpcHost`.
- Interaction handlers: a handler declares its own stage and priority, and a
  module registers it as an `IInteractionHandler`. `ServerComposition`
  registers the built-ins and the host `IInteractionHandlerRegistry`.
  `InteractionHandlerEntryPoint` builds the registry and `IStatResolver` from
  every registration at startup, so a module that adds a handler or stat
  modifier source may load in any order.
- Statistics: `StatisticsComposition.RegisterStatistics()`
  (`WaywardBeyond.Gameplay/Statistics/StatisticsComposition.cs:17`) registers
  the built-in modifier sources and `IStatResolver` idempotently for both
  client and server compositions. Modules and mods register custom modifier
  sources through `container.RegisterStatModifierSource<T>()`
  (`StatisticsComposition.cs:33`).

## Registration policies

| Policy | Meaning | Used for |
|---|---|---|
| `Keep` | first registration wins | shared singletons and defaults |
| `Replace` | last registration wins | embedding overrides |
| `AppendNewImplementation` | keeps all registrations | ordered or multi-value services |

`Keep` makes shared entry points idempotent. Any module can call them, in any
order.

A world container is a child of the root. DryIoc children only inherit a parent
singleton that the parent has already created, so `RegisterServer` resolves the
shared services a world graph consumes before any world container exists. Add a
new shared singleton to that list, or a world silently gets a private copy.

## Order contracts

- Module load order comes from `config/modules.toml`. The client module loads
  before the server module, so client overrides register first. Do not rely on
  load order where `Keep` or `Replace` can express the intent.
- The client registers its file parsers before config loading.
  `VirtualFileParseService` captures the parser set when it is constructed.
  Config loading resolves it. A parser registered after the first resolution is
  invisible to later parses.
- Server world systems tick in registration order. See
  [networking-worlds](networking-worlds.md).

## Source of truth

- `WaywardBeyond.Server/ServerModule.cs`
- `WaywardBeyond.Server/HostComposition.cs`
- `WaywardBeyond.Server/ServerComposition.cs`
- `WaywardBeyond.Networking/NetworkingComposition.cs`
- `WaywardBeyond.Config/ConfigComposition.cs`
- `WaywardBeyond.Data/DataComposition.cs`
- `WaywardBeyond.Data/Blueprints/BlueprintComposition.cs`
- `WaywardBeyond.Data/Localization/LocalizationComposition.cs`
- `WaywardBeyond.Permissions/PermissionsComposition.cs`
- `WaywardBeyond.Gameplay/Statistics/StatisticsComposition.cs`
- `WaywardBeyond.Client/Injector.cs`
- `WaywardBeyond.Client/Networking/ClientRpcHost.cs`
- `WaywardBeyond.Server.Launcher/Program.cs`
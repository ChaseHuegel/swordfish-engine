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
`container.RegisterTags()` because `LoadScreen` and `NameGenerator` read
localized tags. The dedicated launcher registers no services. It applies CLI
overrides after the engine builds
(`WaywardBeyond.Server.Launcher/Program.cs`).

## Project-owned composition entries

| Project | Entry point | Owns |
|---|---|---|
| `WaywardBeyond.Networking` | `RegisterNetworking()` | `NetworkRegistry` and the built-in wire components |
| `WaywardBeyond.Config` | `RegisterNetworkingConfig()` | `network.toml` config, its sub-config views, and validation |
| `WaywardBeyond.Data` | `RegisterDataServices()` | shared data services such as `Migrator` |
| `WaywardBeyond.Data` (localization) | `LocalizationComposition.RegisterTags()` | localized tag database and parser |
| `WaywardBeyond.Server` | `RegisterHost()`, `RegisterServer()` | host wire-up, server composition |
| `WaywardBeyond.Bricks` | `Injector` | brick database, registry, parser |
| `WaywardBeyond.Items` | `Injector` | item database and parser |
| `WaywardBeyond.Bodies` | `Injector` | body database, parser |
| `WaywardBeyond.Skills` | `Injector` | skill database, parser, tags |
| `WaywardBeyond.Permissions` | `RegisterPermissions()` | permission file parser, loader, and compiled policy |
| `WaywardBeyond.Gameplay` | none | no registrations. `ServerComposition` registers its one DI type, `IInteractionHandlerRegistry`, and the default handlers |

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
- Interaction handlers: `ServerComposition` registers the shared
  `IInteractionHandlerRegistry` as a host singleton and registers the default
  handlers in stage order. A mod resolves it from the host container at startup
  and registers its handlers, so a module that adds a handler must load after
  the server module. The registry is not owned by a Gameplay composition entry,
  because Gameplay has no other registrations and does not reference DryIoc.

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
- Server world systems tick in registration order. See
  [networking-worlds](networking-worlds.md).

## Source of truth

- `WaywardBeyond.Server/ServerModule.cs`
- `WaywardBeyond.Server/HostComposition.cs`
- `WaywardBeyond.Server/ServerComposition.cs`
- `WaywardBeyond.Networking/NetworkingComposition.cs`
- `WaywardBeyond.Config/ConfigComposition.cs`
- `WaywardBeyond.Data/DataComposition.cs`
- `WaywardBeyond.Data/Localization/LocalizationComposition.cs`
- `WaywardBeyond.Permissions/PermissionsComposition.cs`
- `WaywardBeyond.Client/Injector.cs`
- `WaywardBeyond.Client/Networking/ClientRpcHost.cs`
- `WaywardBeyond.Server.Launcher/Program.cs`
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
calls `container.RegisterDataServices()`.

The client module (`WaywardBeyond.Client/Injector.cs`) registers client-only
services and the embedding's content choices. The dedicated launcher registers
no services. It applies CLI overrides after the engine builds
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
| `WaywardBeyond.Bodies` | `Injector` | body database, parser |
| `WaywardBeyond.Skills` | `Injector` | skill database, parser, tags |

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
- Interaction content: `ServerComposition` registers `ServerInteractionContent`
  as the default. The client module replaces it with `ClientInteractionContent`.

## Registration policies

| Policy | Meaning | Used for |
|---|---|---|
| `Keep` | first registration wins | shared singletons and defaults |
| `Replace` | last registration wins | embedding overrides |
| `AppendNewImplementation` | keeps all registrations | ordered or multi-value services |

`Keep` makes shared entry points idempotent. Any module can call them, in any
order.

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
- `WaywardBeyond.Client/Injector.cs`
- `WaywardBeyond.Client/Networking/ClientRpcHost.cs`
- `WaywardBeyond.Server.Launcher/Program.cs`
# Config Schema

One subject: the TOML config schemas for modules and launchers. (Asset content
configs live in [asset-definitions](asset-definitions.md); networking config in
[networking-transports](networking-transports.md).)

## `manifest.toml` (per module)

One per module (e.g. `Swordfish/manifest.toml`,
`WaywardBeyond.Client.Core/manifest.toml`). Schema:
`Shoal/Modularity/ModuleManifest.cs`.

| Key | Type | Required | Purpose |
|---|---|---|---|
| `ID` | string | yes | unique module id |
| `Name` | string | yes | display name |
| `Description` | string | no | |
| `Author` | string | no | |
| `Website` | string | no | |
| `Source` | string | no | |
| `RootPathOverride` | PathInfo | no | override module root |
| `ScriptsPath` | PathInfo | no | scripts location |
| `AssembliesPath` | PathInfo | no | assemblies location |
| `Assemblies` | string[] | yes | DLLs that form the module |

Example:

```toml
ID = "waywardbeyond.client.core"
Name = "Wayward Beyond"
Description = "The space sandbox RPG."
Assemblies = [
    "WaywardBeyond.Client.Core.dll",
]
```

Manifest files require `CopyToOutputDirectory=Always` in the `.csproj`. Never
remove it.

## `modules.toml` (per launcher)

One per launcher (e.g. `Swordfish.Launcher/assets/config/modules.toml`,
`WaywardBeyond.Client.Launcher/config/modules.toml`). Defines module load order
and script-compilation permission. Schema: `Shoal/Modularity/ModuleOptions.cs`.
Loader: `Shoal/Modularity/ModulesLoader.cs`.

| Key | Type | Purpose |
|---|---|---|
| `AllowScriptCompilation` | bool | allow C# scripting |
| `LoadOrder` | string[] | module ids in load order |

Example:

```toml
AllowScriptCompilation = false
LoadOrder = [
    "swordfish",
    "waywardbeyond.client.core",
]
```

## `chat.toml` (per app)

Runtime chat tunables. Registered by the client module via
`RegisterConfig<ChatSettings>`. Defaults live in
`WaywardBeyond.Shared.Config/ChatSettings.cs`. See [chat](chat.md).

| Key | Type | Default | Purpose |
|---|---|---|---|
| `TimeoutSeconds` | int | 10 | seconds the closed chat overlay lingers after last activity |
| `MaxHistory` | int | 100 | client scrollback message capacity |

## `ui.toml` (per app)

Runtime UI tunables. Registered via `RegisterConfig<UISettings>`.

| Key | Type | Default | Purpose |
|---|---|---|---|
| `NameplateDistance` | int | 32 | remote player name tag render distance in world units; 0 disables tags (settings page control: 0-64, steps of 8) |

## `physics.toml` (per app)

Runtime physics tunables. Registered via `RegisterConfig<PhysicsSettings>` by
the engine (`Swordfish/EngineContainer.cs:115`) and the shared host wire-up
(`WaywardBeyond.Server.Core/HostComposition.cs:80`). Defaults live in
`Swordfish/Settings/PhysicsSettings.cs`.

| Key | Type | Default | Purpose |
|---|---|---|---|
| `AccumulateUpdates` | bool | `true` | whether physics steps accumulate to catch up a lagging world |
| `Gravity` | float[3] | `[0, -9.81, 0]` | world gravity as `[x, y, z]`; zero-G runtimes set it to `[0, 0, 0]` |

`gravity` rides a dedicated mapper because Tomlet cannot map
`System.Numerics.Vector3` itself:
`Swordfish.Library/Serialization/Toml/Mappers/Vector3DataBindingTomlMapper.cs`.
Applying gravity happens on change, not per tick:
`Swordfish/Physics/Jolt/JoltPhysicsSystem.cs:93-97` (constructor + `Changed`).

## `network.toml` last-used endpoint

`NetworkingSettings.DefaultHost`/`DefaultConnectPort` double as the **last-used
endpoint**: the multiplayer page writes the entered address/port to them on
every connect attempt and persists them (`network.toml`, `SettingsManager`
save path), and the page prefill reads them back on launch. Singleplayer never
writes them, so they cannot encode how a session was joined — the mode marker
lives in `profile.toml` (`LastServerMode`).

## `storage.toml` (per app)

Save-data root, registered via `RegisterConfig<StorageSettings>`.
Schema: `WaywardBeyond.Shared.Config/StorageSettings.cs`.

| Key | Type | Default | Purpose |
|---|---|---|---|
| `DataRoot` | string | `saves/` | root for `profile.db` and the per-level databases; relative paths resolve against the process working directory |

A dedicated server can override the root with `--data`.

## `profile.toml` (per app)

Client-local state and preferences, registered via `RegisterConfig<ProfileSettings>`.
Schema: `WaywardBeyond.Client.Core/Configuration/ProfileSettings.cs`.

| Key | Type | Default | Purpose |
|---|---|---|---|
| `LastServerMode` | enum | `Local` | how the last session was joined (`Local` \| `Remote`); Continue branches on this |
| `SavedServers` | `SavedServer[]` | `[]` | saved connect targets, each `{ Name, Host, Port }`; deduped by host:port, capped at 32 |

## How configs load

TOML parsing via Tomlet. See `Shoal/Modularity/ModuleOptions.cs`,
`Shoal/Modularity/ModuleManifest.cs`, and the registrations in
`Shoal/AppEngine.cs:165-169`. `Shoal.Extensions.Swordfish/TomletExtensions.cs`
provides TOML helpers.

## Conventions

- Any new tunable is a config key in a TOML config, never a hardcoded constant.
- When you add a config key, update this table in the same change.

## Source of truth

- `Shoal/Modularity/ModuleManifest.cs`
- `Shoal/Modularity/ModuleOptions.cs`
- `Shoal/Modularity/ModulesLoader.cs`
- Launcher `modules.toml` files

## Tests that pin this

- `Swordfish.Tests` exercise manifest/module loading (via `TestBase` container).
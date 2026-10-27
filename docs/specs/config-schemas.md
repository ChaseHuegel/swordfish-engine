# Config Schema

One subject: the TOML config schemas for modules and launchers. (Asset content
configs live in [asset-definitions](asset-definitions.md); networking config in
[networking-transports](networking-transports.md).)

## `manifest.toml` (per module)

One per module (e.g. `Swordfish/manifest.toml`,
`WaywardBeyond.Client/manifest.toml`). Schema:
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
ID = "waywardbeyond.client"
Name = "Wayward Beyond"
Description = "The space sandbox RPG."
Assemblies = [
    "WaywardBeyond.Client.dll",
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
    "waywardbeyond.client",
]
```

## `chat.toml` (per app)

Runtime chat tunables. Registered by the client module via
`RegisterConfig<ChatConfig>`. Defaults live in
`WaywardBeyond.Config/ChatConfig.cs`. See [chat](chat.md).

| Key | Type | Default | Purpose |
|---|---|---|---|
| `StaleMs` | int | `10000` | milliseconds until new chats become stale; the closed chat overlay lingers this long after activity |
| `MaxHistory` | int | `100` | client scrollback message capacity |

## `ui.toml` (per app)

Runtime UI tunables. Registered via `RegisterConfig<UISettings>`.

| Key | Type | Default | Purpose |
|---|---|---|---|
| `NameplateDistance` | int | 32 | remote player name tag render distance in world units; 0 disables tags (settings page control: 0-64, steps of 4) |

## `gameplay.toml` (per app)

Gameplay tunables shared by the client and the server, registered via
`RegisterConfig<GameplayConfig>` (client `Injector.cs`, server
`ServerComposition.cs`; the first registration wins). Schema:
`WaywardBeyond.Config/GameplayConfig.cs`.

| Key | Type | Default | Purpose |
|---|---|---|---|
| `Autosave` | bool | `true` | enables the client character timer and the server level autosave |
| `AutosaveIntervalMs` | int | `300000` | autosave cadence in milliseconds; the settings page uses preset minute intervals |
| `ControlHints` | bool | `true` | show the control hints overlay |
| `Crosshair` | bool | `true` | show the crosshair |
| `DefaultSpawnPosition` | float[3] | `[0, 1, 5]` | the position a character spawns at; the level-creation spawn row derives from it |

## `levelgen.toml` (per app)

Server-side level generation tunables, registered via
`RegisterConfig<LevelGeneratorConfig>` (`WaywardBeyond.Server/ServerComposition.cs`).
Schema: `WaywardBeyond.Config/LevelGeneratorConfig.cs`. The `Asteroid` sub-table
maps `AsteroidConfig` (`WaywardBeyond.Config/AsteroidConfig.cs`), registered by a
`RegisterDelegate` from the config, like the `NetworkingConfig` sub-configs.

Generation is deterministic for a seed plus the values present when a level is
created; changing tuning after creation does not rewrite existing levels.

`LevelGeneratorConfig.Validate` runs at startup and throws on tuning that cannot
produce a level: a count or radius below 1, an inverted radius range, a
non-positive world span, a `RadiusMinDivisor` below 1, non-positive noise
frequency or amplitude, or an empty material list. `AsteroidMaxRadius` is
inclusive. A material that is not a known brick fails level creation instead of
emitting air, and placement stops after 100 candidates per asteroid rather than
spinning.

| Key | Type | Default | Purpose |
|---|---|---|---|
| `AsteroidCount` | int | `20` | number of asteroid structures generated per level |
| `AsteroidMinRadius` | int | `10` | minimum asteroid radius |
| `AsteroidMaxRadius` | int | `75` | maximum asteroid radius |
| `WorldHeight` | int | `100` | world half-span along the Y axis |
| `WorldSpan` | int | `300` | world half-span along the X and Z axes |
| `Asteroid.NoiseOctaves` | int | `2` | simplex noise octaves per sample |
| `Asteroid.NoiseFrequencyMin` | float | `0.02` | base noise frequency |
| `Asteroid.NoiseFrequencyRange` | float | `0.03` | random frequency added per world |
| `Asteroid.NoiseAmplitudeMin` | float | `0.2` | base noise amplitude |
| `Asteroid.NoiseAmplitudeRange` | float | `0.5` | random amplitude added per world |
| `Asteroid.NoiseOffsetRange` | int | `1000` | range of the per-world noise offset |
| `Asteroid.RadiusMinDivisor` | int | `5` | divides an asteroid's width to bound its random radius |
| `Asteroid.Materials` | string[] | `["wb:rock", "wb:ice"]` | brick names a generated asteroid may be built from |

## `physics.toml` (per app)

Runtime physics tunables. Registered via `RegisterConfig<PhysicsSettings>` by
the engine (`Swordfish/EngineContainer.cs:118`) and by the shared server host
(`WaywardBeyond.Server/HostComposition.cs`), so a dedicated server loads it
without an engine container. Defaults live in `Swordfish/Settings/PhysicsSettings.cs`.

| Key | Type | Default | Purpose |
|---|---|---|---|
| `AccumulateUpdates` | bool | `true` | whether physics steps accumulate to catch up a lagging world |
| `FixedTimestamp` | float | `0.016` | the fixed timestep of the physics solver in seconds; changes apply on the next tick |
| `Gravity` | float[3] | `[0, -9.81, 0]` | world gravity as `[x, y, z]`; zero-G runtimes set it to `[0, 0, 0]` |

`gravity` rides a dedicated mapper because Tomlet cannot map
`System.Numerics.Vector3` itself:
`Swordfish.Library/Serialization/Toml/Mappers/Vector3DataBindingTomlMapper.cs`.
Applying gravity happens on change, not per tick:
`Swordfish/Physics/Jolt/JoltPhysicsSystem.cs:105-111` (constructor + `Changed`).

## `network.toml` last-used endpoint

`NetworkingConfig.RemoteHost`/`RemotePort` double as the **last-used
endpoint**: the multiplayer page writes the entered address/port to them on
every connect attempt and persists them (`network.toml`, `SettingsManager`
save path), and the page prefill reads them back on launch. Singleplayer never
writes them, so they cannot encode how a session was joined — the mode marker
lives in `profile.toml` (`LastServerMode`).

`NetworkingConfig` groups its keys into the nested `[Server]`, `[Discovery]`,
`[Transport]`, and `[Protocol]` tables. The full key list lives in
[networking-transports](networking-transports.md).

The dedicated server ships its own `config/network.toml` with LAN defaults.
`--name`/`--port` override it in memory for that run only; the launcher never
rewrites the shipped file (see [networking-transports](networking-transports.md)).

## `storage.toml` (per app)

Save-data root, registered via `RegisterConfig<StorageConfig>`.
Schema: `WaywardBeyond.Config/StorageConfig.cs`.

| Key | Type | Default | Purpose |
|---|---|---|---|
| `SaveRoot` | PathInfo | `saves/` | root for `profile.db` and the per-level databases; relative paths resolve against the process working directory |

A dedicated server can override the root with `--data`.

## `profile.toml` (per app)

Client-local state and preferences, registered via `RegisterConfig<ProfileSettings>`.
Schema: `WaywardBeyond.Client/Configuration/ProfileSettings.cs`.

| Key | Type | Default | Purpose |
|---|---|---|---|
| `LastServerMode` | enum | `Local` | how the last played session was joined (`Local` \| `Remote`); Continue branches on this |
| `LastServerHost` | string | `""` | remote host of the last played session |
| `LastServerPort` | int | `0` | remote port of the last played session |
| `LastSaveGuid` | string | `""` | level GUID of the last played save |
| `LastSaveName` | string | `""` | display name of the last played save, for the Continue card before the listing loads |
| `LastSaveSeed` | int | `0` | seed of the last played save, for the Continue card before the listing loads |
| `LastSaveGameMode` | enum | `Creative` | game mode of the last played save, for the Continue card before the listing loads |
| `LastSaveLastPlayedMs` | long | `0` | last played stamp of the last played save, for the Continue card before the listing loads |
| `LastSaveAgeMs` | long | `0` | accumulated playtime of the last played save, for the Continue card before the listing loads |
| `LastCharacterUuid` | string | `""` | character UUID of the last played colonist |
| `UserId` | string | `""` | stable local user id; generated on first use and sent as the connection hello permission claim |
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
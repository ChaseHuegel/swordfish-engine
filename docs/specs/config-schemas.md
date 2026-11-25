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

Manifest files require `CopyToOutputDirectory=PreserveNewest` in the `.csproj`. Never
remove `CopyToOutputDirectory` from manifest files.

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
lives in `profile.toml` (`LastSession.Server.Mode`).

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
| `SaveRoot` | PathInfo | `saves/` | root for `profile.db` and the per-level databases. Relative paths resolve against `AppPaths.DataRoot` (local user data on clients, working directory on dedicated servers). Rooted paths stay unchanged. |

A dedicated server can override the root with `--data`.

## `profile.toml` (per app)

Client-local state and preferences, registered via `RegisterConfig<ProfileSettings>`.
Schema: `WaywardBeyond.Client/Configuration/ProfileSettings.cs`.

| Key | Type | Default | Purpose |
|---|---|---|---|
| `UserId` | string | `""` | stable local user id; generated on first use and sent as the connection hello permission claim |
| `SavedServers` | `SavedServer[]` | `[]` | saved connect targets, each `{ Name, Host, Port }`; deduped by host:port, capped at 32 |

The last played session is one group, so Continue reads and clears one place:

| Key | Type | Default | Purpose |
|---|---|---|---|
| `LastSession.CharacterUuid` | string | `""` | character UUID of the last played colonist |
| `LastSession.Server.Mode` | enum | `Local` | how the session was joined (`Local` \| `Remote`); Continue branches on this |
| `LastSession.Server.Host` | string | `""` | remote host, empty for a local session |
| `LastSession.Server.Port` | int | `0` | remote port, zero for a local session |
| `LastSession.Save.Guid` | string | `""` | level GUID; empty means no session is recorded |
| `LastSession.Save.Name` | string | `""` | display name, for the Continue card before the listing loads |
| `LastSession.Save.Seed` | int | `0` | level seed, for the Continue card before the listing loads |
| `LastSession.Save.GameMode` | enum | `Creative` | level game mode, for the Continue card before the listing loads |
| `LastSession.Save.LastPlayedMs` | long | `0` | last played stamp, for the Continue card before the listing loads |
| `LastSession.Save.AgeMs` | long | `0` | accumulated playtime, for the Continue card before the listing loads |

`SessionServer` and `SessionSave` are bound through `RegisterDataBinding`, so
they write as plain values. A `DataBinding<T>` without a registered mapper
writes its private backing field instead: `Mode = { _data = "Remote" }`. Add a
`RegisterDataBinding<T>` line next to `ProfileSettings` for every new binding
type.

## `volume.toml` (per app)

Client audio preferences, registered via `RegisterConfig<VolumeSettings>`.
Schema: `WaywardBeyond.Client/Configuration/VolumeSettings.cs`.
`ConfigManager` applies `OutputDevice` to the engine at startup. See
[audio](audio.md) and [settings-ui](settings-ui.md).

| Key | Type | Default | Purpose |
|---|---|---|---|
| `Master` | float | `0.5` | master volume, 0-1 |
| `Music` | float | `0.5` | music channel volume, 0-1 |
| `Effects` | float | `0.5` | effects channel volume, 0-1 |
| `Interface` | float | `0.5` | interface channel volume, 0-1 |
| `OutputDevice` | string | `""` | preferred playback device name, empty follows the system default |

## `keybindings.toml` (per app)

Non-default client keybinding overrides, registered via
`RegisterConfig<KeybindingSettings>`. Schema:
`WaywardBeyond.Client/Configuration/KeybindingSettings.cs`. `ConfigManager`
reads overrides through `ParseBindings` and applies them to `IShortcutService`
on startup (`WaywardBeyond.Client/Configuration/ConfigManager.cs:62`). Cleared
actions store `"Unbound"` and unbind again at startup. Empty values skip cleanly,
and invalid or unparseable values log a warning and skip cleanly. `SetOverrides`
updates the map when settings apply or on shutdown (`ConfigManager.cs:74`). See
[input-shortcuts](input-shortcuts.md) and [settings-ui](settings-ui.md).

| Key | Type | Default | Purpose |
|---|---|---|---|
| `Bindings` | `Dictionary<string, string>` | `{}` | maps each shortcut id (for example `"Movement/Move forward"`) to its serialized `ShortcutBinding` string (for example `"UpArrow"`, `"Ctrl+S"`, or `"RMB"`) or `"Unbound"` |

## `render.toml` (per app)

Runtime rendering settings, registered via `RegisterConfig<RenderSettings>`.
Schema: `Swordfish/Settings/RenderSettings.cs`.
Sub-tables configure ambient occlusion, bloom, and dynamic shadows.
See [rendering-shadows](rendering-shadows.md) and [rendering-sao](rendering-sao.md).

| Key | Type | Default | Purpose |
|---|---|---|---|
| `AntiAliasing` | enum | `MSAA` | anti-aliasing mode (`None` \| `MSAA`) |
| `Framerate` | int | `0` | target framerate limit; 0 removes the cap |
| `VSync` | bool | `true` | vertical synchronization |
| `FOV` | int | `90` | camera field of view in degrees |
| `NearPlane` | float | `0.1` | camera near clipping distance |
| `FarPlane` | float | `1000.0` | camera far clipping distance |
| `TransparentSortThreshold` | float | `0.25` | minimum batch-local camera movement in units before transparent range indices re-sort |
| `SAO.Enabled` | bool | `true` | enables Scalable Ambient Obscurance |
| `SAO.Samples` | int | `8` | sample count for SAO obscurance passes, cost is linear |
| `SAO.ResolutionScale` | float | `0.5` | SAO buffer size as a fraction of the screen, at least one texel, above 1 supersamples |
| `SAO.Radius` | float | `0.35` | world-space obscurance radius in meters, `0` disables |
| `SAO.Intensity` | float | `1.0` | occlusion strength |
| `SAO.Bias` | float | `0.02` | constant normal bias in meters |
| `SAO.Power` | float | `1.5` | visibility curve exponent |
| `SAO.MaxDistance` | float | `48.0` | view distance in meters where SAO fades out |
| `SAO.FadeRange` | float | `8.0` | fade width in meters, `0` and a collapsed range are hard cuts |
| `SAO.BlurPasses` | int | `2` | separable blur passes, 9 taps each, cost is linear |
| `Bloom.Enabled` | bool | `true` | enables bloom post-processing |
| `Bloom.Passes` | int | `2` | blur levels, clamped to 1-4 |
| `Bloom.Intensity` | float | `2.0` | composite strength, clamped to 0-8 |
| `Bloom.Threshold` | float | `0.0` | emissive brightness needed to bloom in linear HDR space, `>= 0` |
| `Bloom.Softness` | float | `0.0` | width of the threshold knee, clamped to 0-1 |
| `Shadows.Enabled` | bool | `true` | enables dynamic shadow mapping passes |
| `Shadows.Quality` | enum | `Medium` | preset quality (`Off`, `Low`, `Medium`, `High`, `Ultra`) |
| `Shadows.FilterMode` | enum | `Soft` | filtering method (`Sharp` \| `Soft`) |
| `Shadows.Resolution` | int | `1024` | directional shadow map cascade resolution |
| `Shadows.MaxDistance` | float | `128.0` | maximum distance from camera for shadow coverage |
| `Shadows.CascadeCount` | int | `3` | number of directional split cascades (1-4) |
| `Shadows.MaxPointShadows` | int | `2` | maximum shadowed point lights near the camera |
| `Shadows.NormalBias` | float | `0.02` | normal offset bias to eliminate shadow acne |
| `Shadows.DepthBias` | float | `0.005` | slope-scaled depth bias for depth comparisons |
| `Particles.Enabled` | bool | `true` | enables CPU particle simulation and rendering |
| `Particles.MaxParticles` | int | `4096` | maximum live particles across all emitters and bursts |
| `Particles.Density` | float | `1.0` | multiplier for emitter emission rates and burst particle counts, clamped to 0..float.MaxValue |

## `logging.toml` (per app)

Logging and crash handler tunables loaded early by `LoggingConfig.Load` (`Shoal/Logging/LoggingConfig.cs`) before DI or child process launch. See [logging-and-crash-handling](logging-and-crash-handling.md).

| Key | Type | Default | Purpose |
|---|---|---|---|
| `MinLevel` | enum | `Information` | minimum log level written to file and console |
| `FlushIntervalMs` | int | `200` | periodic background flush interval in milliseconds (minimum 25) |
| `SyncFlushMinLevel` | enum | `Error` | minimum log level that forces a synchronous queue drain and file flush |
| `MaxLogFiles` | int | `20` | maximum timestamped session log files retained in `logs/` (`0` disables pruning) |
| `CrashHandler.Enabled` | bool | `true` | enables the out-of-process crash supervisor |
| `CrashHandler.MaxCrashReports` | int | `20` | maximum crash reports retained in `crashes/` |
| `CrashHandler.AlertTailLines` | int | `80` | maximum trailing log lines shown in the crash alert dialog |
| `CrashHandler.AlertTailMaxChars` | int | `16384` | maximum trailing characters shown in the crash alert dialog (minimum 512) |
| `CrashHandler.CollectDumps` | bool | `true` | enables `.NET` runtime crash dump generation (`DOTNET_DbgEnableMiniDump`) |
| `CrashHandler.DumpType` | enum | `Mini` | crash dump detail (`Mini` \| `WithHeap` \| `Triage` \| `Full`) |
| `CrashHandler.MaxCrashDumps` | int | `5` | maximum `.dmp` files retained in `crashes/` |
| `CrashHandler.MaxUploadBytes` | int | `8388608` | maximum compressed attachment size in bytes for crash report uploads |

## How configs load

TOML parsing uses Tomlet. See `Shoal/Modularity/ModuleOptions.cs`,
`Shoal/Modularity/ModuleManifest.cs`, and the registrations in
`Shoal/AppEngine.cs`. `Shoal.Extensions.Swordfish/ContainerExtensions.cs`
registers runtime configs through `RegisterConfig<T>(file, saveLocation)`.

`AppEngine` requires an `AppPaths` value and mounts each module's `assets/`
folder into `VirtualFileSystem` in load order. When `AppPaths.MountInVfs` is
true (client local user data), `AppEngine` mounts `AppPaths.DataRoot` last so
user files override module configs and content. `RegisterConfig<T>` reads from
VFS `config/` first, then falls back to the `config/` folder on disk for
launcher defaults such as `physics.toml` and `network.toml`, then falls back to
`new T()`.

`SaveLocation` (`Swordfish.Library/Configuration/SaveLocation.cs`) controls
where `Config<T>.Save()` writes. `SaveLocation.UserData` (the default) writes
to `AppPaths.ConfigRoot` (`<DataRoot>/config/<file>`). `SaveLocation.Install`
writes to `config/<file>` relative to the working directory.
`Config<T>.Save()` creates missing parent directories before writing. Logs
write to `AppPaths.LogsRoot` (`<DataRoot>/logs`). Crash reports and dumps write
to `AppPaths.CrashesRoot` (`<DataRoot>/crashes`).

## Conventions

- Any new tunable is a config key in a TOML config, never a hardcoded constant.
- When you add a config key, update this table in the same change.

## Source of truth

- `Swordfish.Library/IO/AppPaths.cs`
- `Swordfish.Library/Configuration/Config.cs`
- `Swordfish.Library/Configuration/SaveLocation.cs`
- `Shoal/AppEngine.cs`
- `Shoal.Extensions.Swordfish/ContainerExtensions.cs`
- `Shoal/Modularity/ModuleManifest.cs`
- `Shoal/Modularity/ModuleOptions.cs`
- `Shoal/Modularity/ModulesLoader.cs`
- Launcher `modules.toml` files

## Tests that pin this

- `Swordfish.Tests` exercise manifest/module loading (via `TestBase` container).
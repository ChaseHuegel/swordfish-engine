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
# UI localization and copy

One subject: the localization conventions, thematic vocabulary, and string
mappings for game UI elements.

The primary localization catalog is at
`WaywardBeyond.Client/assets/lang/en/ui.toml:1`.

## Principles

UI copy balances functional clarity with immersive diegetic theme.

- **Functional clarity for technical controls.** Main menu navigation
  (`Singleplayer`, `Multiplayer`, `Settings`, `Exit Game`) and engine settings
  must remain direct and standard. Do not obscure graphics, audio, or display
  controls with lore names.
- **Diegetic theme for game flows.** World creation, character creation, and
  session lobbies use the language of space colonization, telemetry, and
  telecommunications.
- **Casing consistency.** Use Title Case for button actions, modal titles, and
  tab headers. Use sentence case for descriptions, hints, and helper text.
- **No dead keys.** Remove unused legacy strings from previous menu
  iterations.

## Vocabulary rules

| Domain | Standard term | Thematic term | Context |
|---|---|---|---|
| Servers / Hosts | Server | Signal | Server list rows, network endpoints |
| Server Directory | Server List | Subspace Signals | Multiplayer server hub |
| LAN Discovery | LAN Discovery | Local Signals | Local subnet network discovery |
| Direct Connect | Direct Connect | Direct Uplink | Manual IP/port entry |
| Connecting | Connect / Join | Uplink / Establish Uplink | Connection initiation |
| Disconnect | Disconnect | Sever Uplink | Disconnecting from a host |
| Favorites | Favorite | Saved Signal | Pinned / bookmarked endpoints |
| Character Creation | Create character | Register colonist | S.P.A.C.E.R. personnel enrolment |
| Character Action | Create | Register | Form submission CTA |
| Character Body | Body preset | Colonist Frame | Physical frame selection |
| Level / Save | Level / Save | Sector | World instances |
| World Seed | Level seed | Sector Seed | Procedural generation seed |
| World Name | Enter name | Sector designation | World identifier input |

## String mapping specification

### Multiplayer and signals

The multiplayer hub at
`WaywardBeyond.Client/UI/Layers/Menus/Main/MultiplayerPage.cs:1` uses signal and
uplink terminology.

| Key | Current value | New value |
|---|---|---|
| `ui.menu.lanDiscovery` | `LAN Discovery` | `Local Signals` |
| `ui.menu.serverList` | `Server List` | `Subspace Signals` |
| `ui.menu.directConnect` | `Direct Connect` | `Direct Uplink` |
| `ui.field.host` | `Server address` | `Address` |
| `ui.button.scan` | `Scan for LAN servers` | `Scan for local signals` |
| `ui.button.scanShort` | `Scan` | `Scan` |
| `ui.button.connect` | `Connect` | `Uplink` |
| `ui.button.joinServer` | `Join Server` | `Establish Uplink` |
| `ui.button.addFavorite` | `Add favorite` | `Add Signal` |
| `ui.menu.addFavorite` | `Add Favorite` | `Add Signal` |
| `ui.menu.favorites` | `Favorites` | `Saved Signals` |
| `ui.menu.removeFavorite` | `Remove Favorite?` | `Drop Signal?` |
| `ui.menu.removeFavoriteBody` | `Remove {0} from your favorites?` | `Drop {0} from saved signals?` |
| `ui.menu.connecting` | `Connecting to Server` | `Establishing Uplink` |
| `ui.menu.connectingTarget` | `Connecting to {0}` | `Uplinking to {0}` |
| `ui.notification.connect.failed` | `Failed to connect to the server` | `Failed to establish uplink` |
| `ui.notification.connect.hostRequired` | `Enter a server address` | `Enter a signal address` |
| `ui.notification.discover.scanning` | `Scanning for LAN servers...` | `Scanning for local signals...` |
| `ui.notification.discover.unavailable` | `LAN discovery is unavailable` | `Signal discovery is unavailable` |
| `ui.notification.discover.none` | `No servers found` | `No local signals detected` |
| `ui.button.servers` | `Servers` | `Signals` |
| `ui.button.disconnect` | `Disconnect` | `Sever Uplink` |
| `ui.menu.connectedTo` | `Connected to` | `Uplinked to` |
| `ui.continue.connectFailed` | `Could not connect to the last server.` | `Could not uplink to the last host.` |

### Colonist personnel flow

Character management at
`WaywardBeyond.Client/UI/Layers/Menus/Main/SelectCharacterPage.cs:1` and
`WaywardBeyond.Client/UI/Layers/Menus/Main/NewCharacterPage.cs:1` uses official
colonization registration terminology.

| Key | Current value | New value |
|---|---|---|
| `ui.button.newCharacter` | `Create character` | `Register colonist` |
| `ui.button.createCharacter` | `Create` | `Register` |
| `ui.field.characterNameLabel` | `Character name` | `Colonist name` |
| `ui.field.bodyPreset` | `Body preset` | `Colonist Frame` |
| `ui.menu.unspentPoints` | `Unspent Points` | `Unallocated S.P.A.C.E.R. Points` |
| `ui.menu.unspentPointsBody` | `You still have {0} S.P.A.C.E.R. points available. Create the colonist anyway?` | `You still have {0} S.P.A.C.E.R. points available. Register the colonist anyway?` |

### Sectors and world creation

Save selection and generation at
`WaywardBeyond.Client/UI/Layers/Menus/Main/SelectSavePage.cs:1` and
`WaywardBeyond.Client/UI/Layers/Menus/Main/NewSavePage.cs:1`.

| Key | Current value | New value |
|---|---|---|
| `ui.field.worldSeed` | `Level seed` | `Sector Seed` |
| `ui.field.saveName` | `Enter name...` | `Sector designation...` |
| `ui.button.next` | `Next` | `Select Sector` |

### Modals and pause menu

General prompts and pause screen at
`WaywardBeyond.Client/UI/Layers/Menus/Pause/HomePage.cs:1` and
`WaywardBeyond.Client/UI/Layers/Menus/Modal/ConfirmModal.cs:1`.

| Key | Current value | New value |
|---|---|---|
| `ui.button.saveAndExit` | `Save & exit` | `Save & Exit` |
| `ui.menu.confirm` | `Are you sure?` | `Confirm Directive` |
| `ui.menu.confirmBody` | `This action cannot be undone.` | `Warning: This action cannot be undone.` |

### Deprecated keys to remove

The following keys are unreferenced in code and must be deleted from
`WaywardBeyond.Client/assets/lang/en/ui.toml:1`:

- `ui.menu.saves`
- `ui.menu.createSave`
- `ui.button.newGame`
- `ui.button.selectCharacter`
- `ui.menu.characters`
- `ui.savedServer.saved`
- `ui.label.spacer.s`
- `ui.label.spacer.p`
- `ui.label.spacer.a`
- `ui.label.spacer.c`
- `ui.label.spacer.e`
- `ui.label.spacer.r`

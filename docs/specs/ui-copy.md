# UI copy and localization

One subject: the player-facing copy rules, the localization catalogs, and the
language key grammar.

## Catalogs

- `WaywardBeyond.Client/assets/lang/en/ui.toml:1` holds every `ui.*` key.
- `WaywardBeyond.Client/assets/lang/en/notification.toml:1` holds every
  `notification.*` key. These are the toasts and the in-game event notices.
- One prefix per file. Do not add a `ui.*` key to `notification.toml`.
- Keys resolve by literal name through `ILocalization`. `GetRequired` throws
  when a key is missing, so a rename must cover every call site.
  See `WaywardBeyond.Client/Extensions/LocalizationExtensions.cs:12`.
- The other language files (`item.toml`, `brick.toml`, `body.toml`,
  `skills.toml`, `naming.toml`, `tags/*.toml`) are data-driven. This spec does
  not cover them.

## Writing rules

The copy follows the Material style guide. Immersion never outranks clarity.

1. Use sentence case for all UI text: buttons, tabs, panel titles, dialog
   titles, labels, settings rows, and values.
2. Allow three exceptions. Acronyms and proper nouns (`S.P.A.C.E.R.`, `HUD`,
   `FPS`, `MSAA`, `AO`, `OK`). Terminal-style text (the chat header, key hints,
   `<none>`, the creation and load feeds). Creative surfaces (credits, playtest
   notice).
3. Do not end a single sentence with a period. Put periods between sentences.
   Keep the final period only when the value holds two sentences or more.
4. Do not use exclamation marks.
5. Do not add a trailing ellipsis. Use one only for truncated text.
6. Use one noun per concept. See the vocabulary table.
7. Keep first-level surfaces standard. The home menu, the settings page, the
   generic buttons, and the error messages stay direct.
8. Do not put player-facing copy in code. Read it from the catalog. Prefer
   `GetRequired` over a fallback literal.

| Avoid | Prefer |
|---|---|
| `Are you sure?` | `Delete galaxy?` |
| `Failed to create the sector.` | `Could not chart the galaxy` |
| `Connecting to Server` | `Establishing uplink` |
| `Save & Exit` | `Save & exit` |
| `Apply Changes` | `Apply changes` |
| `Enter name...` | `Enter name` |
| `Feedback submission failed, please try again.` | `Could not send feedback. Try again.` |

## Key grammar

A key is `ui.<type>.<subject>.<leaf>`.

- `type` is the surface kind: `button`, `panel`, `dialog`, `field`, `label`,
  `message`, `text`, `hint`, `word`, `unit`, `settings`.
- `subject` is the data concept from the code, never the themed word: `level`,
  `character`, `body`, `server`, `session`, `chat`, `inventory`, `feedback`,
  `credits`, `playtest`.
- `leaf` is the action or the surface facet: `create`, `delete`, `join`,
  `scan`, `placeholder`, `empty`, `body`.
- Omit the subject for a global control: `ui.button.back`, `ui.button.cancel`.
- A tab reuses the `panel` key of the pane it selects.
- Use lower camel case only for one code term: `ui.settings.option.frameLimit`.
- The value carries the theme. The key never does.
- A `notification.*` key uses the same grammar without the `ui.` prefix, for
  example `notification.level.saved`.

| Avoid | Prefer |
|---|---|
| `ui.button.createGalaxy` | `ui.button.level.new` |
| `ui.menu.sectorTelemetry` | `ui.panel.level.detail` |
| `ui.field.host` | `ui.field.server.host` |
| `ui.notification.discover.none` | `ui.message.server.discover.empty` |
| `ui.menu.unspentPointsWarning` | `ui.message.character.unspentPoints` |

## Vocabulary

| Concept | Term | Scope |
|---|---|---|
| A save and its generated world | galaxy | Level list, level form, deletion, toasts |
| A server or host | signal | Server list, connection copy |
| Server list | Subspace signals | Multiplayer panel title |
| LAN discovery | Local signals | Discovery tab |
| Direct connect | Direct uplink | Manual address entry |
| Connect | Uplink / Establish uplink | Connection action |
| Disconnect | Sever uplink | Drop the connection |
| A saved server | Saved signal | Favorites tab |
| A character | colonist | Character flow, character toasts |
| Create a character | Register colonist | Entry action. The submit action is `Register` |
| The colonist's combined skill level | level | `ui.label.character.level`. Never reuse a world term here |
| The world preset type | type | Level form dossier |

A save is a galaxy. One save holds one generated world. Do not call a save a
level, a sector, or a map in player-facing copy.

## Confirm dialogs

The title names the action. The body states the result and the irreversibility.
The confirm label repeats the verb. The cancel label reads `Cancel`. Do not use
`Are you sure?` or `Yes` and `No`.

| Key | Value |
|---|---|
| `ui.dialog.level.delete` | Delete galaxy? |
| `ui.dialog.level.delete.body` | The galaxy and everything in it is removed. This cannot be undone. |
| `ui.dialog.character.delete` | Delete colonist? |
| `ui.dialog.character.delete.body` | The colonist and their progress are removed. This cannot be undone. |
| `ui.dialog.server.remove` | Drop signal? |
| `ui.dialog.server.remove.body` | {0} is removed from your saved signals. You can add it again at any time. |

`ui.dialog.confirm` and `ui.dialog.confirm.body` are the generic fallback. Pass
explicit copy for a new destructive action.

## Immersion boundary

- Standard copy: home menu, settings, generic buttons, validation, errors.
- Themed copy: level flow, character flow, multiplayer hub, HUD flavor.
- Terminal copy: chat header, key hints, the creation and load feeds.

An immersive term must be a close synonym. If a player must learn the term to
use the control, the term is wrong.

## Dead keys

No key may be unreferenced. Check both directions before you finish:

1. Every catalog key appears as a literal in code.
2. Every `"ui.…"` and `"notification.…"` literal in code exists in a catalog.

```bash
comm -23 \
  <(grep -rhoE '"(ui|notification)\.[A-Za-z0-9._]+"' --include=*.cs . \
    | tr -d '"' | grep -vE '^ui\.(toml|bmp)$' | LC_ALL=C sort -u) \
  <(grep -hoE '^"(ui|notification)\.[^"]+"' \
    WaywardBeyond.Client/assets/lang/en/ui.toml \
    WaywardBeyond.Client/assets/lang/en/notification.toml \
    | tr -d '"' | LC_ALL=C sort -u)
```

The first `comm` lists a used key that is not defined. Swap the inputs
(`comm -13`) to list a defined key that is not used. Ignore strings that name a
file, such as `ui.toml`.

The build does not read the catalogs. Nothing fails when a key is missing until
the surface renders. Run the sweep by hand.

## Source

- Catalog: `WaywardBeyond.Client/assets/lang/en/ui.toml:1`
- Toasts: `WaywardBeyond.Client/assets/lang/en/notification.toml:1`
- Lookup: `WaywardBeyond.Client/Extensions/LocalizationExtensions.cs:12`
- Home: `WaywardBeyond.Client/UI/Layers/Menus/Main/HomePage.cs:24`
- Level list: `WaywardBeyond.Client/UI/Layers/Menus/Main/SelectSavePage.cs:25`
- Level form: `WaywardBeyond.Client/UI/Layers/Menus/Main/NewSavePage.cs:24`
- Character list: `WaywardBeyond.Client/UI/Layers/Menus/Main/SelectCharacterPage.cs:23`
- Character form: `WaywardBeyond.Client/UI/Layers/Menus/Main/NewCharacterPage.cs:26`
- Server hub: `WaywardBeyond.Client/UI/Layers/Menus/Main/MultiplayerPage.cs:24`
- Pause: `WaywardBeyond.Client/UI/Layers/Menus/Pause/HomePage.cs:15`
- Confirm: `WaywardBeyond.Client/UI/Layers/Menus/Modal/ConfirmModal.cs:17`
- Add signal: `WaywardBeyond.Client/UI/Layers/Menus/Modal/AddFavoriteModal.cs:16`
- Connecting: `WaywardBeyond.Client/UI/Layers/Menus/Modal/ConnectingModal.cs:15`
- Chrome: `WaywardBeyond.Client/UI/Layers/Menus/Main/MainMenu.cs:20`
- Credits: `WaywardBeyond.Client/UI/Layers/Menus/Main/CreditsPage.cs:15`

## Tests

No test pins the catalog. `dotnet build` does not read `ui.toml`.
`Swordfish.Tests/LocalizationTests.cs` covers string formatting only. The key
sweep stays manual.

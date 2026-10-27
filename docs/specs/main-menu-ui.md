# Main menu UI

One subject: the main menu suite layout, flow, data mapping, and visual rules.

## Flow

`MenuPage` is at `WaywardBeyond.Client/UI/Layers/Menus/Main/MenuPage.cs:3`.

| Page | Opens | Primary action |
|---|---|---|
| Home | Main menu | Continue, Singleplayer, Multiplayer, Settings, Credits, Exit Game |
| SelectSave | Singleplayer or Multiplayer | Launch selected archive, or create a colonist when none exist |
| NewSave | SelectSave | Initialize a sector, then open Crew Manifest |
| SelectCharacter | SelectSave or NewSave | Launch the selected colonist |
| NewCharacter | Crew Manifest or empty Singleplayer | Create a colonist, then return to Crew Manifest |
| Multiplayer | Home | Join a favorite or LAN server |
| Settings | Home or Pause | Keep the existing draft/apply behavior |
| Credits | Home | Return |

The old `CharactersPage` is gone. Continue resumes the last played server, save,
and colonist directly. Singleplayer switches the RPC host to the local loopback,
clears the cached save listing and active save, then opens the save list
immediately. The local listing populates through the normal refresh. A local save
list returns with `Return`. A remote save list returns with `Servers` to the
multiplayer server hub. New Game creates a sector before it opens Crew Manifest.

## Layout

`MenuLayout` is at `WaywardBeyond.Client/UI/Layers/Menus/MenuLayout.cs:10`. It
computes the responsive page size, the narrow breakpoint, and the left-pane
ratio. It does not compute panel or button rects.

Each page builds its body with `Row`, `Column`, and `Filler`. Wide two-pane pages
use a row. Narrow pages use a tab strip above one pane. A footer is a fixed
height row with the Return button, the status, and the Primary button.

Footer buttons use the shared slot from `MenuLayoutExtensions.FooterButton`:

```csharp
Widgets.ActionButtonOptions options = _returnButton.FooterButton(MenuLayout.RETURN_BUTTON_WIDTH);
if (ui.ActionButton("Button_Back", label, options))
```

`Widgets.FooterStatus` draws the status between the footer buttons. Each page
passes its own colour, so a warning status uses `UiTheme.Warning` and an error
uses `UiTheme.Danger`.

- The design base is 1280 by 720.
- The page shrinks with the window and has a 480 pixel minimum width.
- Menu pages have no page-level title. Panel titles and tabs identify the page.
- Panels start 12 pixels below the top of the page.
- Two-pane pages stay side-by-side down to a 900 pixel page width.
- Below 900 pixels, the same pages use a tab strip and one full-width pane.
  The tab strip sits directly on the pane top.
- Settings keeps its own `SettingsLayout`. Do not replace it.

Pages render inside the `TitleMenu` content host. The title texture above the
content stays unchanged.

## Pause menu

The pause home page is a centered column below the title image. It sizes to its
content. It has four buttons. Continue, Settings, and Send feedback form the
first group. A dotted divider separates them from Save & exit.

- Buttons are 264 by 44 pixels.
- The gap between buttons is 10 pixels.
- The gap around the divider is 12 pixels.

## Sector initialization

While a sector is preparing, the progress panel shows the stable `Preparing
sector` line and a rolling terminal feed from the `game_create` tag set.

- The header and feed are left-aligned at the top of the panel.
- The feed keeps five lines.
- A new line appears every 2 seconds.
- New lines fade in over 0.3 seconds.
- The oldest line drops when the feed is full.
- A cyan prompt and cursor mark the live line.
- A divider closes the feed region at the bottom of its column.

## Form validation

Create Character and New Game show one form status centered in the footer.
Errors use `UiTheme.Danger`. Warnings use `UiTheme.Warning`. Required field
errors take priority over optional field warnings.

Create Character:

- Missing name: `Name is required`.
- Duplicate name: `Name is already taken`.
- Unspent points: `S.P.A.C.E.R. points remaining: {0}`.

New Game:

- Creation failed: `Failed to create the sector.`
- Missing name: `Name is required`.
- Duplicate name: `Name is already taken`.
- Missing seed: `A random seed will be used.`

## Visual rules

- `DefaultUIRenderer` sets `UIBuilder.DefaultFontID` to `UiTheme.FontUI`
  (`Salmon Mono 9 Regular`). Menus, HUD, chat, and debug overlays use it.
- `UiTheme` supplies the palette. Do not add raw colors to menu pages.
- `MenuPanel` draws the shared settings-style panel: no outer border, an
  inner hairline, and corner caps.
- `ModalDialog` provides the standard centered modal layout: full-screen scrim,
  centered size constraints, and a borderless `ModalPanel`.
- `ModalPanel` draws no border for dialogs. It keeps the corner caps and has
  no inner hairline.
- `Panel` remains the primitive for special sub-panels.
- `ActionButtonOptions.Menu` gives the amber-cyan menu treatment, including the
  double border and cyan hover accent.
- `ActionButtonOptions.Compact` covers small text actions. `IconButton` is a
  borderless ghost button for dice, trash, chevrons, and social icons. It keeps
  a full square hit target and adds only hover/pressed fills.
- `SelectableCard` covers archive cards, crew cards, and server rows. Selected
  rows use one orange border and a cyan left caret bar, not a double outline.
- `Tab` covers narrow pane switching, Favorites/LAN Discovery, and attached
  section tabs.
- `InfoBox` covers the sector and stat descriptions.
- Create Character centers the name field and dice. The body arrows and
  randomize dice form one control cluster.
- Create Character asks for confirmation when S.P.A.C.E.R points remain. The
  player can still create the colonist.
- Stat rows color the first letter only. Focused rows use orange, unfocused
  rows use cyan. The separate short-name box is gone.
- The S.P.A.C.E.R description sits under the evaluation header. The S.P.A.C.E.R
  total is not shown.
- The selected colonist dossier shows the combined skill level under the name.
- Archive, crew, and saved-server delete actions are trash icons on their list
  rows. A saved-server delete opens a confirmation.
- Primary proceed actions live in the footer: Launch, Initialize, Create, and
  Join Server.

## Data mapping

The mockup uses galaxy terminology. The game stores levels and characters.

- Galactic Archives is the level/save list from `GameSaveService`. It scrolls
  through `Widgets.ScrollArea`, so it shows the same scrollbar as the crew
  manifest when the list overflows.
- Sector Telemetry is `Level` data plus the active `Character` when present.
- Crew Manifest is `ICharacterStorage`.
- Body Preset is the real `Character.Body` ID list from
  `CharacterAssetService`. It replaces the mockup's separate species, gender,
  and suit selectors.
- Favorites is `ProfileSettings.SavedServers`.
- LAN Discovery is `LanDiscoveryService`.
- The server table shows name, address, players, and ping. Ping currently renders
  the unknown state (`-`) until the transport exposes a reading.
- Direct Connect is a left-aligned toolbar with host, port, and Connect.
  Connection errors render centered in the footer between Return and Join
  Server, so the page does not shift.
- Active connection attempts display an input-blocking connecting modal
  (`ConnectingModal.cs`) with an animated scanning signal indicator. The
  player can cancel the attempt with the Cancel button or the Escape key.
  Completion or failure dismisses the modal automatically.
- The Favorites list has an Add Favorite action at the bottom. It opens a modal
  with address, port, Cancel, and Submit.
- LAN rows have a star action that saves the discovered server.
- When connected to a remote server, the server list shows a connected banner
  with View Saves and Disconnect. Disconnect restores the local loopback, clears
  the cached listing and active save, and stays on the server list.
- Ping uses Font Awesome `signal` with color bands: green below 60 ms, yellow
  below 100 ms, orange below 150 ms, red below 250 ms. At 250 ms or more it
  switches to a red `xmark`. Unknown is a dim `signal` plus `-`.

Continue shows a hover card with the last played archive, its session type, its
mode, seed, last played time, time played, and the last colonist's combined
skill level. The type is `Singleplayer` or `Multiplayer`. A multiplayer card
adds one destination line with the saved server name, or the endpoint when no
name matches. The card draws from the current save cache when it holds the last
save, and falls back to the summary recorded in `profile.toml` otherwise. The
combined level is the sum of every recorded skill's resolved level.

Continue is enabled only when a last session is recorded, the recorded colonist
still exists, and the card has an archive name. The chevron, the enabled state,
and the card use that one result, so a disabled button never draws a card and an
enabled button always does. Deleting the last played save or colonist clears the
recorded session.

## Credits

The credits page reads three entries from `ui.toml`:

- `ui.credits.header` holds the kicker and the studio name on two lines.
- `ui.credits.body` holds the three production sections.
- `ui.credits.thanks` holds the playtester thanks.

The renderer splits the header on the newline so the kicker uses the small font
and the studio name uses the title font. The body and thanks render as
paragraphs and size to their measured text height. Markup supplies the colors.
Credits uses a 720 pixel maximum page height. The whole block scrolls inside the
panel with a scrollbar when needed.

## Omitted controls

Do not add fake menu controls.

- Sandbox rules have no per-save engine support.
- Game Mode selection is omitted. Level creation and character creation still
  use Creative mode.
- Keybindings stay deferred by issue 0086.
- Server ping and description columns have no backing data.
- The settings page keeps its existing controls and four categories.

## Source

- Flow: `WaywardBeyond.Client/UI/Layers/Menus/Main/MenuPage.cs:3`
- Layout: `WaywardBeyond.Client/UI/Layers/Menus/MenuLayout.cs:10`
- Main: `WaywardBeyond.Client/UI/Layers/Menus/Main/HomePage.cs:24`
- Continue: `WaywardBeyond.Client/Saves/ContinueService.cs:18`
- Message modal: `WaywardBeyond.Client/UI/Layers/Menus/Modal/MessageModal.cs:13`
- Connecting modal: `WaywardBeyond.Client/UI/Layers/Menus/Modal/ConnectingModal.cs:14`
- Pause: `WaywardBeyond.Client/UI/Layers/Menus/Pause/HomePage.cs:15`
- Select save: `WaywardBeyond.Client/UI/Layers/Menus/Main/SelectSavePage.cs:25`
- New Game: `WaywardBeyond.Client/UI/Layers/Menus/Main/NewSavePage.cs:23`
- Crew Manifest: `WaywardBeyond.Client/UI/Layers/Menus/Main/SelectCharacterPage.cs:23`
- Create Character: `WaywardBeyond.Client/UI/Layers/Menus/Main/NewCharacterPage.cs:25`
- Multiplayer: `WaywardBeyond.Client/UI/Layers/Menus/Main/MultiplayerPage.cs:24`
- Credits: `WaywardBeyond.Client/UI/Layers/Menus/Main/CreditsPage.cs:15`
- Menu chrome: `WaywardBeyond.Client/UI/Layers/Menus/Main/MainMenu.cs:20`
- Widgets: `WaywardBeyond.Client/UI/Widgets/`
- Localization: `WaywardBeyond.Client/assets/lang/en/ui.toml`

## Tests

- `Swordfish.Tests/MenuLayoutTests.cs` covers wide and narrow page geometry and
  the shared footer button slot.
- `Swordfish.Tests/ActionButtonTests.cs` covers fixed and intrinsic buttons.
- `Swordfish.Tests/UiThemeResolverTests.cs` covers menu control states.
- `Swordfish.Tests/ConnectingModalTests.cs` covers connecting modal rendering, scanning signal beam, and cancellation flow.
- `Reef.Tests/UIBuilderDefaultFontTests.cs` covers the default font contract.

# Main menu UI

One subject: the main menu suite layout, flow, data mapping, and visual rules.

## Flow

`MenuPage` is at `WaywardBeyond.Client/UI/Layers/Menus/Main/MenuPage.cs:3`.

| Page | Opens | Primary action |
|---|---|---|
| Home | Main menu | Continue, New Game, Multiplayer, Options, Credits, Exit Game |
| SelectSave | Continue | Launch selected archive, or create a colonist when none exist |
| NewSave | New Game | Initialize a sector, then open Crew Manifest |
| SelectCharacter | Continue or New Game | Launch the selected colonist |
| NewCharacter | Crew Manifest or empty Continue | Create a colonist, then return to Crew Manifest |
| Multiplayer | Home | Join a favorite or LAN server |
| Settings | Home or Pause | Keep the existing draft/apply behavior |
| Credits | Home | Return |

The old `CharactersPage` is gone. Continue goes directly to Crew Manifest, or to
Create Character when no character exists. New Game sets the active save before
it opens Crew Manifest.

## Layout

`MenuLayout` is at `WaywardBeyond.Client/UI/Layers/Menus/MenuLayout.cs:7`. It
computes a centered page with left and right panel rects, a tab strip, a footer,
and Return/Primary footer buttons.

- The design base is 1280 by 720.
- The page shrinks with the window and has a 480 pixel minimum width.
- Menu pages have no page-level title. Panel titles and tabs identify the page.
- Panels start 12 pixels below the top of the page.
- Two-pane pages stay side-by-side down to a 900 pixel page width.
- Below 900 pixels, the same pages use a tab strip and one full-width pane.
- Settings keeps its own `SettingsLayout`. Do not replace it.

Pages render inside the `TitleMenu` content host. The title texture above the
content stays unchanged.

## Pause menu

The pause home page is a centered 300 by 221 pixel block below the title image.
It has four buttons in one column. Continue, Settings, and Send feedback form
the first group. A dotted divider separates them from Save & exit.

- Buttons are 264 by 44 pixels.
- The gap between buttons is 10 pixels.
- The gap around the divider is 12 pixels.
- Continue has the cyan menu chevron.

## Visual rules

- `DefaultUIRenderer` sets `UIBuilder.DefaultFontID` to `UiTheme.FontUI`
  (`Salmon Mono 9 Regular`). Menus, HUD, chat, and debug overlays use it.
- `UiTheme` supplies the palette. Do not add raw colors to menu pages.
- `MenuPanel` draws the shared settings-style panel: no outer border, an
  inner hairline, and corner caps.
- `ModalPanel` draws one outer border for dialogs. It has no inner hairline.
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

- Galactic Archives is the level/save list from `GameSaveService`.
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
- The Favorites list has an Add Favorite action at the bottom. It opens a modal
  with address, port, Cancel, and Submit.
- LAN rows have a star action that saves the discovered server.
- Ping uses Font Awesome `signal` with color bands: green below 60 ms, yellow
  below 100 ms, orange below 150 ms, red below 250 ms. At 250 ms or more it
  switches to a red `xmark`. Unknown is a dim `signal` plus `-`.

Continue shows a hover card with the active or most recent archive, its mode,
seed, last played time, time played, and the active colonist's combined skill
level. The combined level is the sum of every recorded skill's resolved level.

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
- Layout: `WaywardBeyond.Client/UI/Layers/Menus/MenuLayout.cs:7`
- Main: `WaywardBeyond.Client/UI/Layers/Menus/Main/HomePage.cs:21`
- Pause: `WaywardBeyond.Client/UI/Layers/Menus/Pause/HomePage.cs:14`
- Continue: `WaywardBeyond.Client/UI/Layers/Menus/Main/SelectSavePage.cs:26`
- New Game: `WaywardBeyond.Client/UI/Layers/Menus/Main/NewSavePage.cs:17`
- Crew Manifest: `WaywardBeyond.Client/UI/Layers/Menus/Main/SelectCharacterPage.cs:24`
- Create Character: `WaywardBeyond.Client/UI/Layers/Menus/Main/NewCharacterPage.cs:23`
- Multiplayer: `WaywardBeyond.Client/UI/Layers/Menus/Main/MultiplayerPage.cs:22`
- Credits: `WaywardBeyond.Client/UI/Layers/Menus/Main/CreditsPage.cs:14`
- Menu chrome: `WaywardBeyond.Client/UI/Layers/Menus/Main/MainMenu.cs:19`
- Widgets: `WaywardBeyond.Client/UI/Widgets/`
- Localization: `WaywardBeyond.Client/assets/lang/en/ui.toml`

## Tests

- `Swordfish.Tests/MenuLayoutTests.cs` covers wide and narrow geometry.
- `Swordfish.Tests/ActionButtonTests.cs` covers fixed and intrinsic buttons.
- `Swordfish.Tests/UiThemeResolverTests.cs` covers menu control states.
- `Reef.Tests/UIBuilderDefaultFontTests.cs` covers the default font contract.

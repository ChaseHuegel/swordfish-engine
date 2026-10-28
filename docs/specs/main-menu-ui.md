# Main menu UI

One subject: the main menu suite layout, flow, data mapping, and visual rules.

## Flow

`MenuPage` is at `WaywardBeyond.Client/UI/Layers/Menus/Main/MenuPage.cs:3`.

| Page | Opens | Primary action |
|---|---|---|
| Home | Main menu | Continue, Singleplayer, Multiplayer, Settings, Credits, Exit game |
| SelectSave | Singleplayer or Multiplayer | Select the galaxy, or register a colonist when none exist |
| NewSave | SelectSave | Chart a galaxy, then open the crew manifest |
| SelectCharacter | SelectSave or NewSave | Embark the selected colonist |
| NewCharacter | Crew manifest or empty Singleplayer | Register a colonist, then return to the crew manifest |
| Multiplayer | Home | Establish an uplink to a saved or local signal |
| Settings | Home or Pause | Keep the existing draft/apply behavior |
| Credits | Home | Go back |

The old `CharactersPage` is gone. Continue resumes the last played server, save,
and colonist directly. Singleplayer switches the RPC host to the local loopback,
clears the cached save listing and active save, then opens the save list
immediately. The local listing populates through the normal refresh. A local save
list goes back with `Back`. A remote save list goes back with `Signals` to the
multiplayer server hub. Chart new galaxy opens the level form before the crew
manifest.

## Layout

`MenuLayout` is at `WaywardBeyond.Client/UI/Layers/Menus/MenuLayout.cs:10`. It
computes the responsive page size, the narrow breakpoint, and the left-pane
ratio. It does not compute panel or button rects.

Each page builds its body with `Row`, `Column`, and `Filler`. Wide two-pane pages
use a row. Narrow pages use a tab strip above one pane. A footer is a fixed
height row with the Back button, the status, and the Primary button.

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

## Charting a galaxy

While a galaxy is charting, the progress panel shows the stable `Charting
galaxy` line and a rolling terminal feed from the `game_create` tag set.

- The header and feed are left-aligned at the top of the panel.
- The feed keeps five lines.
- A new line appears every 2 seconds.
- New lines fade in over 0.3 seconds.
- The oldest line drops when the feed is full.
- A cyan prompt and cursor mark the live line.
- A divider closes the feed region at the bottom of its column.

## Form validation

The character form and the level form show one form status centered in the
footer. Errors use `UiTheme.Danger`. Warnings use `UiTheme.Warning`. Required
field errors take priority over optional field warnings.

Character form:

- Missing name: `Name is required`.
- Duplicate name: `Name is already taken`.
- Unspent points: `S.P.A.C.E.R. points remaining: {0}`.

Level form:

- Creation failed: `Could not chart the galaxy`.
- Missing name: `Name is required`.
- Duplicate name: `Name is already taken`.
- Missing seed: `A random seed will be used`.

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
- `Tab` covers narrow pane switching, Saved signals/Local signals, and attached
  section tabs.
- `InfoBox` covers the galaxy and stat descriptions.
- The character form centers the name field, its dice, and the body selector
  group. Each selector row holds a muted label, a discrete stepper, and a dice.
- The character form asks for confirmation when S.P.A.C.E.R points remain. The
  player can still register the colonist.
- Stat rows color the first letter only. Focused rows use orange, unfocused
  rows use cyan. The separate short-name box is gone.
- The S.P.A.C.E.R description sits under the evaluation header. The S.P.A.C.E.R
  total is not shown.
- The selected colonist dossier shows the combined skill level and species under
  the name. See [specs/ui-copy](ui-copy.md) for the line format.
- Archive, crew, and saved-signal delete actions are trash icons on their list
  rows. A saved-signal delete opens a confirmation.
- Primary proceed actions live in the footer: Embark, Chart, Register, and
  Establish uplink.

## Data mapping

A save is a galaxy. The game stores levels and characters.

- Galactic archives is the level list from `GameSaveService`. The listing is
  ordered by last played, newest first. It scrolls through `Widgets.ScrollArea`,
  so it shows the same scrollbar as the crew manifest when the list overflows.
- Galaxy telemetry is `Level` data plus the active `Character` when present.
- Crew manifest is `ICharacterStorage`.
- Species and gender are the real body selectors. Each one is a stepper over
  the shipped body definitions, and the pair resolves to the `Character.Body`
  ID through `CharacterAssetService.GetBodyId`. The mockup's suit selector
  stays out because no asset backs it.
- Saved signals is `ProfileSettings.SavedServers`.
- Local signals is `LanDiscoveryService`.
- The server table shows name, address, players, and ping. Ping currently renders
  the unknown state (`-`) until the transport exposes a reading.
- Direct uplink is a left-aligned toolbar with address, port, and Uplink.
  Connection errors render centered in the footer between Back and Establish
  uplink, so the page does not shift.
- Active connection attempts display an input-blocking connecting modal
  (`ConnectingModal.cs`) with an animated scanning signal indicator. The
  player can cancel the attempt with the Cancel button or the Escape key.
  Completion or failure dismisses the modal automatically.
- The Saved signals list has an Add signal action at the bottom. It opens a
  dialog with address, port, Cancel, and Add signal.
- Each signal row draws its action as the final column. Saved signal rows show a
  remove action. Local signal rows show a star action that saves the discovered
  signal.
- When connected to a remote server, the signal list shows a connected banner
  with View archives and Sever uplink. Sever uplink restores the local loopback,
  clears the cached listing and active save, and stays on the signal list.
- Ping uses Font Awesome `signal` with color bands: green below 60 ms, yellow
  below 100 ms, orange below 150 ms, red below 250 ms. At 250 ms or more it
  switches to a red `xmark`. Unknown is a dim `signal` plus `-`.

Continue shows a hover card with the last played galaxy, its session type, its
mode, seed, last played time, time played, and the last colonist's combined
skill level and species. The card hugs its content width and stops at the page
bound, so no line clips. A small gap separates the session type from the galaxy
name. The type is `Singleplayer` or `Multiplayer`.
A multiplayer card adds one destination line with the saved signal name, or the
endpoint when no name matches. The card draws from the current save cache when
it holds the last save, and falls back to the summary recorded in `profile.toml`
otherwise. The combined level is the sum of every recorded skill's resolved
level.

Continue is enabled only when a last session is recorded, the recorded colonist
still exists, and the card has a galaxy name. The chevron, the enabled state,
and the card use that one result, so a disabled button never draws a card and an
enabled button always does. Deleting the last played galaxy or colonist clears
the recorded session.

## Credits

The credits page reads three entries from `ui.toml`:

- `ui.panel.credits` is the panel title.
- `ui.text.credits.body` holds the production sections.
- `ui.text.credits.thanks` holds the playtester thanks.

The body and thanks render as paragraphs and size to their measured text height.
Markup supplies the colors. Credits uses a 720 pixel maximum page height. The
whole block scrolls inside the panel with a scrollbar when needed.

## Omitted controls

Do not add fake menu controls.

- Sandbox rules have no per-save engine support.
- Game Mode selection is omitted. Level creation and colonist registration still
  use Creative mode.
- Keybindings stay deferred by issue 0086.
- Server ping and description columns have no backing data.
- The settings page keeps its existing controls and four categories.

## Source

- Flow: `WaywardBeyond.Client/UI/Layers/Menus/Main/MenuPage.cs:3`
- Layout: `WaywardBeyond.Client/UI/Layers/Menus/MenuLayout.cs:10`
- Main: `WaywardBeyond.Client/UI/Layers/Menus/Main/HomePage.cs:24`
- Continue: `WaywardBeyond.Client/Saves/ContinueService.cs:18`
- Message modal: `WaywardBeyond.Client/UI/Layers/Menus/Modal/MessageModal.cs:14`
- Connecting modal: `WaywardBeyond.Client/UI/Layers/Menus/Modal/ConnectingModal.cs:15`
- Pause: `WaywardBeyond.Client/UI/Layers/Menus/Pause/HomePage.cs:15`
- Select save: `WaywardBeyond.Client/UI/Layers/Menus/Main/SelectSavePage.cs:25`
- Level form: `WaywardBeyond.Client/UI/Layers/Menus/Main/NewSavePage.cs:24`
- Crew manifest: `WaywardBeyond.Client/UI/Layers/Menus/Main/SelectCharacterPage.cs:23`
- Character form: `WaywardBeyond.Client/UI/Layers/Menus/Main/NewCharacterPage.cs:26`
- Multiplayer: `WaywardBeyond.Client/UI/Layers/Menus/Main/MultiplayerPage.cs:24`
- Credits: `WaywardBeyond.Client/UI/Layers/Menus/Main/CreditsPage.cs:15`
- Menu chrome: `WaywardBeyond.Client/UI/Layers/Menus/Main/MainMenu.cs:20`
- Widgets: `WaywardBeyond.Client/UI/Widgets/`
- Copy: [specs/ui-copy](ui-copy.md)

## Tests

- `Swordfish.Tests/MenuLayoutTests.cs` covers wide and narrow page geometry and
  the shared footer button slot.
- `Swordfish.Tests/ActionButtonTests.cs` covers fixed and intrinsic buttons.
- `Swordfish.Tests/UiThemeResolverTests.cs` covers menu control states.
- `Swordfish.Tests/ConnectingModalTests.cs` covers connecting modal rendering, scanning signal beam, and cancellation flow.
- `Reef.Tests/UIBuilderDefaultFontTests.cs` covers the default font contract.

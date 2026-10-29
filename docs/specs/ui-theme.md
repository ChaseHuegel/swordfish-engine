# UI theme and widgets

One subject: the shared game UI theme, metrics, style resolution, and widget
library.

Status: legacy. New UI uses [ui-design-system](ui-design-system.md),
`UI/Theme/Theme.rss`, and `UI/Components/Components.rss`. This document covers
the C# tokens and widgets that remain until the migration completes.

## Scope

The theme and widgets live in `WaywardBeyond.Client`. Reef stays renderer
generic. Do not add game colors or game control styles to Reef.

The theme is static. A page reads `UiTheme` and `UiMetrics` directly. If a second
theme becomes a real requirement, promote these classes to an injected theme at
that time.

## Theme tokens

`UiTheme` is at `WaywardBeyond.Client/UI/Theme/UiTheme.cs:6`. It holds the font
IDs and semantic colors:

- fonts: `FONT_UI` (`Salmon Mono 9 Regular`), `FONT_ICONS`, and `FONT_BRANDS`.
- surfaces: `PanelBackground`, `PanelSolid`, `PanelInner`, `InputBackground`,
  `InputFocus`, `ControlBackground`, `SelectionBackground`.
- borders: `Border`, `BorderInner`, `BorderHover`.
- text: `Text`, `TextMuted`, `TextDim`, `TextSoft`.
- accents: `Accent`, `AccentBright`, `AccentDark`.
- primary actions: `Primary`, `PrimaryBright`, `PrimaryDark`.
- status: `Danger`, `DangerBright`, `DangerDark`, `Success`, `Warning`.
- scrims: `ScreenBackground`, `Scrim`, `ScrimSoft`, `ScrimHeavy`.
- controls: `Track`, `TrackFill`, `Thumb`, `ButtonBackground`, `ButtonHover`,
  `RowSelected`, `Transparent`.

Use semantic tokens, not component names, in new code. Do not add a new color
literal to a page or a widget.

`UIBuilder.DefaultFontID` at `Reef/UIBuilder.cs:155` applies `FONT_UI` to game
text that does not specify a font. `DefaultUIRenderer` sets it once at startup.
An explicit `FontOptions.ID` still wins. This keeps menus, HUD, chat, and debug
overlays on the same pixel font.

`UiMetrics` is at `WaywardBeyond.Client/UI/Theme/UiMetrics.cs:4`. It holds font
sizes, control heights, button sizes, spacing, border sizes, and scroll metrics.
The menu suite adds `MENU_TITLE_FONT`, `MENU_BODY_FONT`, `MENU_SMALL_FONT`,
`MENU_LABEL_FONT`, and the shared panel cap sizes.

Constants are `UPPER_SNAKE_CASE`, per [development](../development.md).

`SettingsMetrics` is at
`WaywardBeyond.Client/UI/Settings/SettingsMetrics.cs:4`. It holds only geometry
that belongs to the settings page, such as the tray size and footer button
widths.

## Style resolution

`ControlStyle` is at `WaywardBeyond.Client/UI/Theme/ControlStyle.cs:7`. It holds
the resolved fill, border, border thickness, corner caps, and text color.

`UiThemeResolver` is at `WaywardBeyond.Client/UI/Theme/UiThemeResolver.cs:6`. It
turns a variant and a state into a `ControlStyle`. The resolver covers button
variants, segmented cells, and stepper arrows. It has no drawing code, so tests
can call it directly.

One `ButtonPalette` per variant holds the fill, border, and text colours for the
normal, hover, pressed, and active states. `FromPalette` maps a state onto those
colours, so a variant is added by adding data, not by copying a control-style
block.

`ButtonVariant` is at `WaywardBeyond.Client/UI/Theme/ButtonVariant.cs:4`. The
variants are `Secondary`, `Primary`, `Subtle`, and `Danger`.

## Widgets

The widgets are extension methods on `UIBuilder<Material>` in
`WaywardBeyond.Client/UI/Widgets/`.

| Widget | Source | Purpose |
|---|---|---|
| `Panel` | `Widgets.Panel.cs:14` | Clipped panel with border, caps, and optional inner hairline. |
| `ActionButton` | `Widgets.ActionButton.cs:15` | Text action button with layout constraints, a fixed rect, or an intrinsic size. |
| `IconButton` | `Widgets.IconButton.cs:12` | Borderless icon action with hover/pressed fills. |
| `PingIndicator` | `Widgets.Ping.cs:45` | Colored signal icon and ping value. |
| `Toggle` | `Widgets.Toggle.cs:16` | Two-cell on/off control. |
| `SegmentedControl` | `Widgets.Toggle.cs:39` | Radio-like choice row. |
| `Stepper` | `Widgets.Stepper.cs:14` | Discrete value control. |
| `Slider` | `Widgets.Slider.cs:14` | Continuous value control. |
| `Scrollbar` | `Widgets.Scrollbar.cs:13` | Non-interactive list indicator that fills the layout or a fixed rect. |
| `ScrollArea` | `Widgets.ScrollArea.cs:14` | Clipped wheel-scroll area with a scrollbar and routed wheel input. |
| `ModalDialog` | `Widgets.ModalDialog.cs:31` | Centered dialog scope holding the scrim, center container, and modal panel. |
| `MenuTextBox` | `Widgets.MenuTextBox.cs:13` | Clipped menu text box that returns its interactions. |
| `DottedDivider` | `Widgets.DottedDivider.cs:12` | Light horizontal dotted separator. |
| `FooterStatus` | `Widgets.FooterStatus.cs:13` | Centered footer status between two action buttons. |
| `ModalPanel` | `Widgets.MenuPanel.cs:39` | Borderless panel for modal dialogs. |
| `MenuPanel` | `Widgets.MenuPanel.cs:61` | Shared menu panel with inner hairline, corner caps, and optional title. Content hugs when the width is unconstrained. |
| `InfoBox` | `Widgets.InfoBox.cs:12` | Titled box with wrapped body text. Highlight prefixes need a space after the color token. |
| `SelectableCard` | `Widgets.SelectableCard.cs:14` | Selected/hover card with one orange border and a cyan left caret. |
| `StatLabel` | `Widgets.StatLabel.cs:10` | Stat name with a colored first letter. |
| `Tab` | `Widgets.Tab.cs:12` | Attached tab with selected state. |
| `Label` | `Widgets.Text.cs:13` | Single-line text in the current layout or a fixed rect. |
| `Paragraph` | `Widgets.Text.cs:57` | Wrapped text in the current layout or a fixed rect. |
| `Divider` | `Widgets.Text.cs:90` | One-pixel divider. |
| `Row` / `Column` | `Widgets.Layout.cs:76` | Horizontal or vertical stack with spacing. |
| `Spacer` / `Filler` | `Widgets.Layout.cs:106` | Fixed or remaining-space filler. |
| `Page` | `Widgets.Layout.cs:52` | Centered page with a fixed maximum size and a clip. |

`ControlResult<T>` at `WaywardBeyond.Client/UI/Widgets/ControlResultOfT.cs:4` is
the return type of `Toggle`, `SegmentedControl`, `Stepper`, and `Slider`. Read
`Changed` before `Value`, so a control never needs an `out` value.

`Widgets.ActionButton.cs:253` defines `ActionButtonOptions`. Use the factories:

- `Menu` for a full menu button. Width is `UiMetrics.MenuButtonMinWidth`, height
  is `UiMetrics.MenuButtonHeight`.
- `Compact` for a small action button.
- `ListRow` for a full-width list row. Content aligns left.

Set `Layout` to use the button in a row or a column. The layout width and height
override the factory defaults. `MenuLayoutExtensions.FooterButton` supplies the
shared footer slot.

Icon-only actions use `Widgets.IconButton` with `IconButtonOptions`, not the text
button factories. Icon buttons are borderless and transparent at rest, then use a
hover/pressed fill and icon-color change. Danger icons use the danger palette.
Keep the full square hit target and let the glyph center in it.

A text button can take a fixed rect, layout constraints, or an intrinsic size.
Prefer `Layout` for pages. Pass `ActionButtonOptions` as one value rather than
building a partial options value at the call site.

Set `Selected` to true for a current list row. Set `Anchors` for placement in a
parent layout. Pass a `SoundEffectService` to play the standard click sound.

## Page rules

- Menus use `ActionButton` for actions. Use `Primary` for the main action,
  `Secondary` for normal actions, `Subtle` for Back, and `Danger` for
  destructive actions.
- Lists use `ActionButtonOptions.ListRow`. Use `Selected` for the active item.
- Text inputs use `Widgets.MenuTextBox` inside a menu, or `Widgets.TextBox`
  directly. Both read theme colors for focus, placeholder, selection, and caret.
  A container that draws its own frame, such as the chat send bar, sets
  `TextBoxState.Options.Chrome` to false.
- HUD overlays (`Hotbar`, `Bars`, `Actions`, `ShapeSelector`,
  `OrientationSelector`, `ControlHints`, `ChatLayer`) use theme tokens, borders,
  and corner caps directly. `ChatLayer` uses `Widgets.Panel` and
  `Widgets.Scrollbar` when open. The modal `Inventory` window uses
  `Widgets.MenuPanel` and `Widgets.DottedDivider`.
- Settings controls use the common widgets. The block layout stays in
  `SettingsRenderer`.

## Source

- Theme: `WaywardBeyond.Client/UI/Theme/UiTheme.cs:6`
- Metrics: `WaywardBeyond.Client/UI/Theme/UiMetrics.cs:4`
- Glyphs: `WaywardBeyond.Client/UI/MenuGlyphs.cs:4`
- Resolver: `WaywardBeyond.Client/UI/Theme/UiThemeResolver.cs:6`
- Widgets: `WaywardBeyond.Client/UI/Widgets/`
- Menu layout: `WaywardBeyond.Client/UI/Layers/Menus/MenuLayout.cs:10`
- Settings layout: `WaywardBeyond.Client/UI/Settings/SettingsLayout.cs:6`
- Settings renderer: `WaywardBeyond.Client/UI/Settings/SettingsRenderer.cs:14`

## Tests

- `Swordfish.Tests/UiThemeResolverTests.cs` covers button variants, ghost icon
  states, disabled styles, segmented cells, and stepper arrows.
- `Swordfish.Tests/ActionButtonTests.cs` covers fixed and intrinsic button
  sizing plus Font Awesome glyph centering.
- `Swordfish.Tests/MenuLayoutTests.cs` covers wide and narrow page geometry and
  the shared footer button slot.
- `Swordfish.Tests/PingDisplayTests.cs` covers ping bands and unknown/unreachable states.
- `Swordfish.Tests/MenuPanelRenderTests.cs` checks that menu panels have no outer
  border, that selected cards use one border plus a cyan caret, and that panels
  with an unconstrained width hug their content up to `MaxWidth`.
- `Swordfish.Tests/TextBoxZOrderTests.cs` checks selection and caret paint
  order with the theme colors, plus relative text-box sizing.
- `Reef.Tests/UIBuilderDefaultFontTests.cs` covers the default font contract.
- `Reef.Tests/UIBuilderZOrderTests.cs` checks the Reef paint-order contract.

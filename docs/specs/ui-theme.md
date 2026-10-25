# UI theme and widgets

One subject: the shared game UI theme, metrics, style resolution, and widget
library.

## Scope

The theme and widgets live in `WaywardBeyond.Client`. Reef stays renderer
generic. Do not add game colors or game control styles to Reef.

The theme is static. A page reads `UiTheme` and `UiMetrics` directly. If a second
theme becomes a real requirement, promote these classes to an injected theme at
that time.

## Theme tokens

`UiTheme` is at `WaywardBeyond.Client/UI/Theme/UiTheme.cs:6`. It holds the font
IDs and semantic colors:

- fonts: `FontUI` (`Salmon Mono 9 Regular`), `FontIcons`, and `FontBrands`;
- surfaces: `PanelBackground`, `PanelSolid`, `PanelInner`, `InputBackground`,
  `InputFocus`, `ControlBackground`, `SelectionBackground`;

- borders: `Border`, `BorderInner`, `BorderHover`;
- text: `Text`, `TextMuted`, `TextDim`, `TextSoft`;
- accents: `Accent`, `AccentBright`, `AccentDark`;
- primary actions: `Primary`, `PrimaryBright`, `PrimaryDark`;
- status: `Danger`, `DangerBright`, `DangerDark`, `Success`, `Warning`;
- scrims: `ScreenBackground`, `Scrim`, `ScrimSoft`, `ScrimHeavy`;
- HUD: `HudPanel`, `SlotBackground`, `Slot`, `SlotSelected`, `SlotDragging`;
- controls: `Track`, `TrackFill`, `Thumb`, `ButtonBackground`, `ButtonHover`,
  `RowSelected`, `Transparent`.

Use semantic tokens, not component names, in new code. Do not add a new color
literal to a page or a widget.

`UIBuilder.DefaultFontID` at `Reef/UIBuilder.cs:155` applies `FontUI` to game
text that does not specify a font. `DefaultUIRenderer` sets it once at startup.
An explicit `FontOptions.ID` still wins. This keeps menus, HUD, chat, and debug
overlays on the same pixel font.

`UiMetrics` is at `WaywardBeyond.Client/UI/Theme/UiMetrics.cs:4`. It holds font
sizes, control heights, button sizes, spacing, border sizes, and scroll metrics.
The menu suite adds `MenuTitleFont`, `MenuBodyFont`, `MenuSmallFont`,
`MenuLabelFont`, and the shared panel cap sizes.

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

`ButtonVariant` is at `WaywardBeyond.Client/UI/Theme/ButtonVariant.cs:4`. The
variants are `Secondary`, `Primary`, `Subtle`, and `Danger`.

## Widgets

The widgets are extension methods on `UIBuilder<Material>` in
`WaywardBeyond.Client/UI/Widgets/`.

| Widget | Source | Purpose |
|---|---|---|
| `Panel` | `Widgets.Panel.cs:14` | Clipped panel with border, caps, and optional inner hairline. |
| `ActionButton` | `Widgets.ActionButton.cs:31` | Fixed or intrinsic text action button. |
| `IconButton` | `Widgets.IconButton.cs:12` | Borderless icon action with hover/pressed fills. |
| `Toggle` | `Widgets.Toggle.cs:16` | Two-cell on/off control. |
| `SegmentedControl` | `Widgets.Toggle.cs:39` | Radio-like choice row. |
| `Stepper` | `Widgets.Stepper.cs:14` | Discrete value control. |
| `Slider` | `Widgets.Slider.cs:14` | Continuous value control. |
| `Scrollbar` | `Widgets.Scrollbar.cs:13` | Non-interactive list indicator. |
| `MenuPanel` | `Widgets.MenuPanel.cs:24` | Shared menu panel with inner hairline, corner caps, and optional title. |
| `InfoBox` | `Widgets.InfoBox.cs:13` | Titled box with wrapped body text. |
| `SelectableCard` | `Widgets.SelectableCard.cs:14` | Selected/hover card with custom content. |
| `Tab` | `Widgets.Tab.cs:12` | Attached tab with selected state. |
| `Label` | `Widgets.Text.cs:13` | Fixed-rect single-line text. |
| `Paragraph` | `Widgets.Text.cs:48` | Fixed-rect wrapped text. |
| `Divider` | `Widgets.Text.cs:81` | One-pixel divider. |

`Widgets.ActionButton.cs:166` defines `ActionButtonOptions`. Use the factories:

- `Menu` for a full menu button. Width is `UiMetrics.MenuButtonMinWidth`, height
  is `UiMetrics.MenuButtonHeight`.
- `Compact` for a small action button.
- `ListRow` for a full-width list row. Content aligns left.

Icon-only actions use `Widgets.IconButton` with `IconButtonOptions`, not the text
button factories. Icon buttons are borderless and transparent at rest, then use a
hover/pressed fill and icon-color change. Danger icons use the danger palette.
Keep the full square hit target and let the glyph center in it.

A text button gets a fixed rect through the `ActionButton` overload. A button
without a fixed rect uses its `MinWidth`, `Height`, and padding to size itself.

Set `Selected` to true for a current list row. Set `Anchors` for placement in a
parent layout. Pass a `SoundEffectService` to play the standard click sound.

## Page rules

- Menus use `ActionButton` for actions. Use `Primary` for the main action,
  `Secondary` for normal actions, `Subtle` for Back, and `Danger` for
  destructive actions.
- Lists use `ActionButtonOptions.ListRow`. Use `Selected` for the active item.
- Text inputs use `Widgets.TextBox`. It reads theme colors for focus,
  placeholder, selection, and caret.
- HUD elements use theme tokens for color and font size. Do not force HUD
  elements into menu panels or buttons. HUD layout stays local.
- Settings controls use the common widgets. The block layout stays in
  `SettingsRenderer`.

## Source

- Theme: `WaywardBeyond.Client/UI/Theme/UiTheme.cs:6`
- Metrics: `WaywardBeyond.Client/UI/Theme/UiMetrics.cs:4`
- Resolver: `WaywardBeyond.Client/UI/Theme/UiThemeResolver.cs:6`
- Widgets: `WaywardBeyond.Client/UI/Widgets/`
- Menu layout: `WaywardBeyond.Client/UI/Layers/Menus/MenuLayout.cs:7`
- Settings layout: `WaywardBeyond.Client/UI/Settings/SettingsLayout.cs:7`
- Settings renderer: `WaywardBeyond.Client/UI/Settings/SettingsRenderer.cs:16`

## Tests

- `Swordfish.Tests/UiThemeResolverTests.cs` covers button variants, ghost icon
  states, disabled styles, segmented cells, and stepper arrows.
- `Swordfish.Tests/ActionButtonTests.cs` covers fixed and intrinsic button
  sizing plus Font Awesome glyph centering.
- `Swordfish.Tests/MenuLayoutTests.cs` covers wide, narrow, and footer geometry.
- `Swordfish.Tests/TextBoxZOrderTests.cs` checks selection and caret paint
  order with the theme colors, plus relative text-box sizing.
- `Reef.Tests/UIBuilderDefaultFontTests.cs` covers the default font contract.
- `Reef.Tests/UIBuilderZOrderTests.cs` checks the Reef paint-order contract.

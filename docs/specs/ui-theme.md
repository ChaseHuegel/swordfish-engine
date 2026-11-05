# UI theme

One subject: where Wayward Beyond UI gets its colors, fonts, spacing, and
dynamic fade colors.

Status: current. Static visual design lives in Reef stylesheets. C# holds only
the colors that a view must fade at run time.

## Token files

`WaywardBeyond.Client/UI/Theme/Theme.rss:1` holds the design tokens:

- fonts: `--wb-font-ui`, `--wb-font-icons`, and `--wb-font-brands`. Every text
  rule in the stylesheet uses `var()`, so a font change is one token edit. A
  literal font family in `.rss` is a bug.
- text: `--wb-text`, `--wb-text-muted`, `--wb-text-dim`, `--wb-text-soft`, and
  `--wb-text-faint`.
- surfaces: `--wb-panel`, `--wb-panel-solid`, `--wb-panel-inner`,
  `--wb-input`, `--wb-input-focus`, `--wb-control`, `--wb-selected-surface`, and
  `--wb-selection`.
- slots: `--wb-slot-size`, the shared item-slot cell size.
- borders: `--wb-border`, `--wb-border-inner`, and `--wb-border-hover`.
- accents: `--wb-accent`, `--wb-accent-bright`, and `--wb-accent-dark`.
- primary actions: `--wb-primary`, `--wb-primary-bright`, and
  `--wb-primary-dark`.
- status: `--wb-danger`, `--wb-danger-bright`, `--wb-danger-dark`,
  `--wb-success`, and `--wb-warning`.
- star chart: `--wb-chart`, `--wb-chart-label-scrim`, `--wb-star-red`,
  `--wb-star-emerald`, and `--wb-star-violet`. White stars use `--wb-text`, so no cyan or amber class
  collides with `--wb-accent` or `--wb-primary`.
- buttons and scrims: `--wb-button`, `--wb-button-hover`, `--wb-scrim`,
  `--wb-scrim-soft`, and `--wb-scrim-heavy`.

`WaywardBeyond.Client/UI/Components/Components.rss:1` holds the shared classes
that markup selects. The classes cover layout, typography, buttons, panels,
modals, cards, tabs, rows, fields, keycaps, hints, progress, slots,
scrollbars, HUD chrome, and the choice and slider controls.

A class that one page uses lives in a stylesheet beside that page, such as
`ChatView.rss` next to `ChatView.rml`. A page stylesheet declares only classes
with its page prefix and no tokens. Every stylesheet is global at compile time,
so the prefix marks the owner. The compiler does not scope the file.

Item slots take their cell size from `--wb-slot-size` through `.wb-slot`, so a
page sizes slots only when it needs a different cell. `.wb-divider-dashed`
draws the dashed rule from `border-dash`, so a divider costs one element.

Use semantic tokens in the stylesheets. Do not add a color literal to `.rml`
or to a view. A new token belongs in `Theme.rss`. A shared class belongs in
`Components.rss`, and a class used by one page belongs beside that page. The
behavior widgets described below are the one exception, because they own
interaction state.

`DefaultUIRenderer` sets the Reef default font to `Salmon Mono 9 Regular` at
`WaywardBeyond.Client/UI/DefaultUIRenderer.cs:31`. An explicit font still wins.

## Runtime token access

C# does not copy token values. `WaywardBeyond.Client/UI/Theme/Palette.rml`
declares a palette over `Theme.rss`, and the generator emits one constant per
token into `WbPalette`:

```xml
<Reef xmlns="Reef" Class="WbPalette" Palette="wb"
      Namespace="WaywardBeyond.Client.UI.Theme" />
```

- `--wb-text-muted` becomes `WbPalette.TextMuted`.
- Colours are `Vector4`, the font names are `string`, and `--wb-slot-size` is
  an `int`.
- A new token is available to C# without a second declaration. A renamed token
  fails the build where it is used.
- `Swordfish.Tests/PaletteTests` fails when the palette misses a token.

`DynamicColors` at `WaywardBeyond.Client/UI/Theme/DynamicColors.cs:7` keeps
only the helpers that compute at run time:

- `Fade` multiplies the alpha channel by a fade amount.
- `AccentRichText` and `AccentBrightRichText` format rich-text color tags.

The faded token reads come from `WbPalette`, so a view writes
`DynamicColors.Fade(WbPalette.Panel, alpha)`.

These views fade tokens at run time:

- `NotificationView` for the notification panel scrim, accent bar, and text.
- `BarsView` for the progress bar panel, border, dividers, text, and fill tint.
- `ActionsView` for the action strip text.
- `ChatView` for the message background, accent bar, text, and sender run.
- `LoadScreenView` for the status, hint, signal line, segment, and key tags.
- `NewSaveView` for the creating log lines.

The notification panel uses `--wb-scrim-soft` alone. It darkens the scene behind
the text, so it does not multiply the scrim by a panel color. A component-wise
color product is not a blend, and it discards one of the inputs.

Do not add a literal colour to a view. Add a token to `Theme.rss` instead.

## Behavior widgets

Markup owns structure and static visuals. A few controls still need C# for
editing, drag, or pointer mechanics:

| Control | Source | Used by |
|---|---|---|
| `TextBox` | `WaywardBeyond.Client/UI/Widgets/Widgets.TextBox.cs:41` | `TextInputWidget` |
| `SegmentedControl` | `WaywardBeyond.Client/UI/Widgets/Widgets.Toggle.cs:24` | `SettingsChoiceWidget` and the scroll repro fixture |
| `Slider` | `WaywardBeyond.Client/UI/Widgets/Widgets.Slider.cs:21` | `SettingsSliderWidget` |
| `ActionButton` | `WaywardBeyond.Client/UI/Widgets/Widgets.ActionButton.cs:23` | `Slider` |

`ControlResult<T>` at `WaywardBeyond.Client/UI/Widgets/ControlResultOfT.cs:4`
reports a change and the new value. Read `Changed` before `Value`.

These widgets keep literal colors and sizes equal to the matching `Theme.rss`
tokens and `Components.rss` classes. Markup cannot own their interaction
state, so the values stay beside the drawing code.

Sound and pointer helpers live in `WaywardBeyond.Client/UI/WidgetInteraction.cs:7`.
They play the interface sounds and decode `Interactions` flags.

## Button variants

`ButtonVariant` at `WaywardBeyond.Client/UI/Components/ButtonVariant.cs:4` has
four members: `Secondary`, `Primary`, `Subtle`, and `Danger`.

`ButtonParameters` at `WaywardBeyond.Client/UI/Components/ButtonParameters.cs:4`
maps the variant to booleans for `Button.rml`. Markup passes the variant with
`param:variant`:

```xml
<Button param:variant="primary" param:enabled="@context.CanSubmit">
    <Text class="wb-button-label" text="@L('ui.button.send')" />
</Button>
```

The parameter model accepts the lowercase names `secondary`, `primary`,
`subtle`, and `danger`.

## Tests

- `Swordfish.Tests/ButtonStateTests.cs` covers disabled button styles.
- `Swordfish.Tests/SettingsInteractionTests.cs` covers selected settings rows.
- `Swordfish.Tests/SettingsPageRenderTests.cs` covers the reset row danger
  border.
- `Swordfish.Tests/ConnectingModalTests.cs` covers the accent scan beam.
- `Swordfish.Tests/PauseScrimTests.cs` covers the pause scrim.
- `Swordfish.Tests/TextBoxZOrderTests.cs` covers text box paint order.
- `Swordfish.Tests/SliderInteractionTests.cs` covers slider clicks.
- `Reef.Tests/UIBuilderDefaultFontTests.cs` covers the default font contract.
- `Reef.Tests/UIBuilderZOrderTests.cs` checks the Reef paint-order contract.

# UI theme

One subject: where Wayward Beyond UI gets its colors, fonts, spacing, and
dynamic fade colors.

Status: current. Static visual design lives in Reef stylesheets. C# holds only
the colors that a view must fade at run time.

## Token files

`WaywardBeyond.Client/UI/Theme/Theme.rss:1` holds the design tokens:

- fonts: `--wb-font-ui`, `--wb-font-icons`, and `--wb-font-brands`.
- text: `--wb-text`, `--wb-text-muted`, `--wb-text-dim`, and `--wb-text-soft`.
- surfaces: `--wb-panel`, `--wb-panel-solid`, `--wb-panel-inner`,
  `--wb-input`, `--wb-input-focus`, `--wb-control`, and `--wb-selection`.
- borders: `--wb-border`, `--wb-border-inner`, and `--wb-border-hover`.
- accents: `--wb-accent`, `--wb-accent-bright`, and `--wb-accent-dark`.
- primary actions: `--wb-primary`, `--wb-primary-bright`, and
  `--wb-primary-dark`.
- status: `--wb-danger`, `--wb-danger-bright`, `--wb-danger-dark`,
  `--wb-success`, and `--wb-warning`.
- buttons and scrims: `--wb-button`, `--wb-button-hover`, `--wb-scrim`,
  `--wb-scrim-soft`, and `--wb-scrim-heavy`.

`WaywardBeyond.Client/UI/Components/Components.rss:1` holds the classes that
markup selects. The classes cover layout, typography, buttons, panels, modals,
cards, tabs, rows, fields, badges, keycaps, hints, progress, slots, scrollbars,
settings, chat, inventory, and HUD chrome.

Use semantic tokens in the stylesheets. Do not add a color literal to `.rml`
or to a view. A new token belongs in `Theme.rss` and a new class in
`Components.rss`. The behavior widgets described below are the one exception,
because they own interaction state.

`DefaultUIRenderer` sets the Reef default font to `Salmon Mono 9 Regular` at
`WaywardBeyond.Client/UI/DefaultUIRenderer.cs:31`. An explicit font still wins.

## Dynamic colors

`DynamicColors` at `WaywardBeyond.Client/UI/Theme/DynamicColors.cs:7` is the
only shared C# color source. It holds literal copies of the tokens that a view
multiplies by an alpha value that comes from runtime state:

- `Text`, `TextMuted`, `PanelBackground`, `PanelInner`, `Border`, `Track`,
  `Accent`, `AccentBright`, and `ScrimSoft`.
- `Fade` multiplies the alpha channel by a fade amount.
- `AccentRichText` and `AccentBrightRichText` format rich-text color tags.

These views use it:

- `NotificationView` for the notification panel, accent bar, and text.
- `BarsView` for the progress bar panel, border, track, fill, and text.
- `ActionsView` for the action strip text.
- `ChatView` for the message background, accent bar, text, and sender run.
- `LoadScreenView` for the status, hint, signal line, segment, and key tags.
- `NewSaveView` for the creating log lines.

Do not add a general theme class. Add a token here only when a view must fade
it at run time.

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

# UI design system

One subject: how to author Wayward Beyond UI with Reef markup and styles.

## Roles

| Place | Owns |
|---|---|
| `.rss` | Tokens, colors, fonts, spacing, variants, pseudo-states, responsive classes |
| `.rml` | Structure, content, layout, slots, visibility, localization keys |
| View code-behind | `L`, glyphs, formatting, event forwarding, service access |
| Page or layer | Services, game state, navigation, sounds, async work, persistence |
| Context type | Dynamic data only |
| Behavior widget | Input mechanics only |

Do not put static copy in code-behind. Do not put colors or layout values in a
context type. Do not put page services in a view.

## Localization

Views derive from `UiViewBase`.

```csharp
internal partial class HomePageView : UiViewBase
{
    public HomePageView(ILocalization localization) : base(localization)
    {
    }
}
```

Markup uses keys:

```xml
<Text class="wb-button-label" text="@L('ui.button.home.continue')" />
```

Dynamic keys use the same method:

```xml
<Text text="@L(row.LabelKey)" />
```

Settings labels belong in `.rml`. A data model may carry values and option keys.
It must not carry localized display strings.

## State

Prefer `readonly record struct` for display state. Use a reference type only
when two-way binding requires it.

State types carry only dynamic data. They do not carry colors, sizes, localized
strings, or style names.

Wire view callbacks once in a constructor. Do not allocate lambdas every frame.

## Styling

Use `Theme.rss` for tokens. Keep shared classes in `Components.rss`, and keep
classes that one page uses in a stylesheet beside that page. Name every page
class with the page prefix, and declare no tokens in a page stylesheet.

- Prefix game variables and classes with `wb-`.
- Use semantic classes, not page-specific colors.
- Use the `Button` component with `param:variant` instead of C# palettes.
  Supported variants are `secondary`, `primary`, `danger`, and `subtle`.
- Use `color` for fills. The generator maps `background-color` to both the fill
  and the background color. Legacy panels, buttons, tabs, and dividers paint the
  fill only, so a background color can add an unintended layer.
- Use `background-color` only for real overlays such as scrims and field frames.
- Use `text-color` to recolor child text and icons.
- Use `border-bottom` and the other side borders for attached tab or underline
  treatments. Use `border-dash` for dashed rules such as `.wb-divider-dashed`.
- Do not put raw colors or font sizes in migrated `.rml` files.

## Components and classes

Use a component when the concept has structure, slots, clipping, or repeated
composition.

The shared `Button` component owns the outer border, optional inner border,
corner caps, padding, disabled state, and compact state. Use
`param:doubleBorder="false"` only for single-outline buttons such as the
playtest close button. Use `param:compact="true"` for compact controls. Use
`param:enabled="@expression"` for the disabled visual state. Keep the normal
`disabled="@expression"` attribute as well. It guards the click callbacks.
Disabled buttons keep their disabled fill, border, text color, and cap style
while hovered or pressed.

`Panel` owns the outer caps, the inner hairline, and the content inset. Use
`param:loose="true"` for the 24-pixel inset used by the credits page. `Tab`
owns the selected bottom bar. `Card` owns the selected caret.

### Component parameters

Use typed parameters when a component has a small set of visual variants. The
parameter type carries values only. It must not carry localized strings,
colors, or page services.

```xml
<Reef xmlns="Reef"
      xmlns:param="Reef.Param"
      Class="FeedbackView"
      Context="FeedbackState">
    <Button param:variant="primary"
            param:enabled="@context.CanSubmit"
            onclick="OnSubmit">
        <Text class="wb-button-label" text="@L('ui.button.send')" />
    </Button>
</Reef>
```

Use `param:` to separate component parameters from normal attributes. Local
components are resolved first. Cross-assembly parameter forwarding is a later
step.

`Page` is the menu frame. Pass `param:maxWidth`, `param:maxHeight`,
`param:minWidth`, and `param:minHeight` for the bounds. The component owns the
screen inset: `param:insetX` is 60 pixels on each side and `param:insetY` is 120
pixels on the top and bottom. The settings shell passes `insetX="40"`. The
`topInset` flag adds the 12-pixel top inset. A page takes its size from the
viewport, not from its parent, so host padding such as the `TitleMenu` inset
does not shrink it. A page does not compute the inset.

A responsive page renders one body. The body uses `.wb-split` for the wide row
and `.wb-split-narrow` for the narrow column. The tab strip is visible only
when narrow. A panel is visible in the wide layout, or when its tab is
selected. Panels use `.wb-split-third`, `.wb-split-half`, or `.wb-split-fill`
for the width, and the fill class wins when narrow.

`Modal` is the modal panel. Wrap it with the `.wb-scrim-*` and
`.wb-modal-center` classes. Pass `param:size` to select a size preset:
`connecting`, `compact`, `form`, `feedback`, or `playtest`. The `auto` default
leaves the size to caller constraints. Caller classes still apply to the panel.

Use a style class when the concept only selects layout, color, typography, or a
state.

Do not create alias components for `Row`, `Column`, `Stack`, `Divider`,
`FooterStatus`, or `StatLabel`. Their behavior is already a Reef primitive or a
style rule.

Lists use `ForEach` with the row's own spacing. Item structure belongs in a
component such as `Card`. A generic list template is not supported.

The foundation also provides `ProgressBar`, `ScrollArea`, `ScrollBar`, and
`ItemSlot`. Scroll areas use `ScrollAreaState` for viewport metrics, content
height, thumb geometry, and drag progress. A page calls `ScrollAreaState.Measure`
with the viewport id. The content element id is the viewport id plus `Content`.
Dynamic fill and thumb sizes use `IConstraint` properties on the state.

`ProgressBarState` carries the value and an optional fill tint, and it falls
back to `--wb-accent`. The component projects a `Content` slot over the track,
so a caller can draw segment dividers without copying the track or fill.

`InfoBox` takes a `title` slot and a body slot. The title text uses the
`.wb-info-box-label` class.

`EmptyState` sets the dim tone for its content. The caller supplies the text
element, because a slot does not inherit font size or alignment.

`FormField` takes a `label` slot and the frame as the default slot. It owns the
four-pixel gap between the label and the frame, so a page adds no spacer
element.

`PageFooter` takes `back`, `status`, and `primary` slots. It owns the row
height, the spacing, and the flexible middle box. A page that needs another
height passes the height as a caller constraint.

`SettingsRows` takes a `SettingsRowsState` context. It renders the section,
choice, stepper, slider, and reset branches once. The state carries the
localization and the row callbacks.

`StatRow` owns one labelled value row. It takes a `label` slot and a `value`
slot in a single horizontal row.

## Text input

`TextInput` is a game-local external widget. The editing surface needs direct
input, caret, selection, and clipboard handling, so it cannot be pure markup.

The field layout and copy stay in `.rml`. The widget accepts `Style` for its
frame and text. `TextInputPalette.Default` supplies selection, caret, and
placeholder colors.

```xml
<Element class="wb-field" layout="vertical" width="fill">
    <Text class="wb-field-label" text="@L('ui.field.feedback.contact')" />
    <wb:TextInput id="TextBox_Contact"
                  bind="context.Contact"
                  input-service="@InputService"
                  placeholder="@L('ui.field.feedback.contact.placeholder')"
                  class="wb-field-frame"
                  width="fill"
                  height="32" />
</Element>
```

## Responsive layout

- Prefer `fill`, `fit`, `shrink`, `min-*`, and `max-*`.
- Give table columns relative widths with `min-width`. Let one column fill the
  remainder, and do not subtract a hand-counted viewport width.
- Breakpoints live on `UiViewBase`. `UsesCompactLayout` splits at 900,
  `UsesStandardLayout` at 1020, and `UsesWideLayout` at 1220. Markup calls the
  helper and never writes the width.
- Use parent padding instead of computed offsets. The page frame is the
  exception, because the `TitleMenu` host already pads. A page sized from its
  parent would shrink by that padding.
- Keep fixed sizes only for icons, glyphs, slot cells, hit targets, and
  animation geometry.
- Use `ui.Width` conditions or dynamic classes for page breakpoints.
- Anchored overlays, such as the Home Continue card, use `overlay="true"` with
  `x` and `y`. Size the wrapper and its frame with `ui.Width` expressions when
  the content is wider than the anchor. A component call does not accept
  expressions for `width` or `height`. Set them on an element instead.
- Labels center vertically by default through the shared text classes. Wrapped
  paragraphs opt out with `vertical-align="top"`, matching legacy `Paragraph`
  behavior.
- `Panel` insets its header and content by 12px. A page that needs another
  inset writes the frame inline with `wb-panel` and `wb-panel-inner`, then sets
  `padding` on the inner element. An attribute `padding` replaces the class
  padding rather than adding to it.
- Do not compute page width or height in C#.
- Allow C# geometry only for algorithmic layout such as radial menus, drag
  previews, and text measurement.

## Behavior widgets

A behavior widget is allowed only when markup cannot express the behavior. It
must accept `Style` and `FontOptions`, and it must not hardcode theme values.

## Tests

- Component gallery goldens cover normal, hover, active, focus, disabled, and
  selected states.
- Page goldens cover narrow and wide sizes.
- Legacy component and page goldens prove parity with the old C# UI. The parity
  comparison ignores instance IDs and clips, and it treats equal fill and
  background colors as one fill.
- Tests check localization keys, layout bounds, callbacks, and responsive
  branches.
- Existing behavior tests remain until the behavior changes.

## Related docs

- [UI architecture](ui-architecture.md)
- [UI architecture plan](ui-architecture-plan.md)
- [UI design-system migration plan](ui-design-system-migration-plan.md)
- [UI remediation plan](ui-remediation-plan.md)
- [Reef markup](reef-markup.md)
- [UI copy](ui-copy.md)
- [Code review](code-review.md)

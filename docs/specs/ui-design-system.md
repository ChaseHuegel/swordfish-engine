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

Use `Theme.rss` for tokens and `Components.rss` for classes.

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
  treatments.
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

`Modal` is the modal panel. Wrap it with the `.wb-scrim-*` and
`.wb-modal-center` classes. Caller constraints and classes apply to the panel,
so each modal controls its own minimum and maximum size.

Use a style class when the concept only selects layout, color, typography, or a
state.

Do not create alias components for `Row`, `Column`, `Stack`, `Divider`,
`FooterStatus`, or `StatLabel`. Their behavior is already a Reef primitive or a
style rule.

Lists use `.wb-list` and `ForEach`. Item structure belongs in a component such
as `Card` or `SelectableRow`. A generic list template is not supported.

The foundation also provides `ProgressBar`, `ScrollArea`, `ScrollBar`, and
`ItemSlot`. Scroll areas use `ScrollAreaState` for viewport metrics, content
height, and thumb geometry. Dynamic fill and thumb sizes use `IConstraint`
properties on the state.

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
- Use parent padding instead of computed offsets.
- Keep fixed sizes only for icons, glyphs, slot cells, hit targets, and
  animation geometry.
- Use `ui.Width` conditions or dynamic classes for page breakpoints.
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

- [UI design-system migration plan](ui-design-system-migration-plan.md)
- [Reef markup](reef-markup.md)
- [UI copy](ui-copy.md)
- [Code review](code-review.md)

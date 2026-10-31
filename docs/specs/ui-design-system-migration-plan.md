# UI design-system migration plan

One subject: the locked plan for moving Wayward Beyond UI to Reef markup and
style classes.

Status: approved and in progress. This document records the target
architecture, the component rules, and the migration phases. Migrated pages
pass the legacy visual parity gate.

## Goals

- Centralize visual design in `Theme.rss` and `Components.rss`.
- Make `.rml` the normal place for structure and designer-facing content.
- Keep state models small and focused.
- Prefer constraints and responsive classes over fixed rectangles.
- Keep every screen visually stable through the migration.
- Build a small component library that can later move to a Reef package.

## Authoring model

| Place | Owns | Must not own |
|---|---|---|
| `.rss` | Tokens, colors, fonts, spacing, variants, pseudo-states, responsive classes | Structure, copy, services |
| `.rml` | Structure, content, layout, slots, visibility, localization keys | Business logic, services, data lookup |
| View code-behind | `L`, glyph lookup, formatting, event forwarding, service access | Layout, colors, page services |
| Page or layer | Services, game state, navigation, sounds, async work, persistence | Visual structure |
| Context type | Dynamic data only | Localized strings, colors, layout values, theme state |
| Behavior widget | Input and editing mechanics only | Hardcoded theme values |

Static labels, headings, hints, and button text live in `.rml` as
`@L('key')`. Settings rows are authored explicitly in `.rml` so label keys stay
designer-facing.

Behavior widgets are allowed when `.rml` cannot express the behavior. They must
accept `Style` and `FontOptions`, and they must not hardcode colors or metrics.

## Locked decisions

| Topic | Decision |
|---|---|
| Foundation location | `WaywardBeyond.Client/UI/Components` first, promote later |
| Theme files | `Theme.rss` and `Components.rss` |
| Parent text color | Add inheritable `text-color` in Reef |
| Per-side borders | Add in Reef for tab underlines and similar rules |
| Component parameters | Typed parameter model via `Parameters`, called with `param:` attributes |
| Parameter resolution | Local components first, cross-assembly later |
| Parameterized slots | Deferred |
| Generic list component | Deferred |
| Text input | Game-local external widget in `WaywardBeyond.Client/UI/Controls` |
| Text input state | Stays in `WaywardBeyond.Client` as `TextBoxState` |
| Text input styling | `Style` for frame, text, font, border, and states |
| Text input extra colors | `TextInputPalette` for selection, caret, and placeholder |
| Text input services | `IInputService` passed from the view |
| No-regression gate | Baseline each screen before migration |
| First slice | Baseline harness and design-system pilot |

## Component rules

Build a component when the concept has structure, slots, clipping, or repeated
composition. Use a style class when the concept only selects layout, color,
typography, or a state.

Components:

- `Button`
- `Modal`
- `Panel`
- `Page`
- `Card`
- `SelectableRow`
- `EmptyState`
- `InfoBox`
- `HintPill`
- `Keycap`
- `ProgressBar`
- `ItemSlot`
- `ScrollArea`
- `ScrollBar`
- `DottedDivider`
- `Stepper`
- `Tab`
- `Notification`


Style classes:

- `.wb-row`
- `.wb-column`
- `.wb-stack`
- `.wb-button`
- `.wb-button-primary`
- `.wb-button-secondary`
- `.wb-button-danger`
- `.wb-button-subtle`
- `.wb-icon-button`
- `.wb-tab`
- `.wb-list`
- `.wb-text`
- `.wb-text-title`
- `.wb-text-muted`
- `.wb-field`
- `.wb-field-frame`
- `.wb-badge`
- `.wb-keycap`
- `.wb-divider`
- `.wb-footer-status`
- `.wb-scrollbar`

`Row`, `Column`, `Stack`, `Divider`, `FooterStatus`, and `StatLabel` are style
classes, not components. Their behavior is already provided by Reef layout and
text primitives.

`List` starts as a style class plus `ForEach` plus `SelectableRow`. A true
generic list needs parameterized slots. That feature is deferred.

## Component parameters

A component can declare `Parameters="ButtonParameters"`. Calls pass values
with the `param:` namespace. The parameter model is a small record or struct
with defaults. It holds visual variants and state values only. It does not hold
localized strings, colors, layout values, or services.

```xml
<Button param:variant="primary" param:enabled="@context.CanSubmit">
    <Text class="wb-button-label" text="@L('ui.button.send')" />
</Button>
```

Use local component parameters first. Cross-assembly forwarding is a later
step.

## Text input

`TextInput` is a game-local external widget. `.rml` owns the field layout,
label, hint, and validation copy. The widget owns editing, selection, caret,
focus, and submit state.

Markup calls the widget with the `controls` prefix:

```xml
<controls:TextInput id="TextBox_SaveName"
                    bind="NameState"
                    input-service="@InputService"
                    placeholder="@L('ui.field.level.name.placeholder')"
                    class="wb-field-frame"
                    width="fill" height="fill" font-size="18" />
```

- `bind` names a `TextBoxState` field on the view. The page reads the text
  from that field. A bind into a value-type context is rejected, because the
  writeback would be lost.
- `input-service` names an `IInputService` property on the view.
- `class="wb-field-frame"` supplies the fill, border, and text colors. The
  widget palette supplies selection, caret, and placeholder colors.

The widget accepts `Style` for frame and text styling. The selection, caret, and
placeholder colors come from `TextInputPalette.Default`. The view passes
`IInputService` for clipboard access only.

The first implementation does not add Reef text state or a Reef text control.

## Responsive rules

- Prefer `fill`, `fit`, `shrink`, `min-*`, and `max-*`.
- Use parent padding instead of computed offsets.
- Keep fixed sizes only for icons, glyphs, slot cells, hit targets, and
  animation geometry.
- Page breakpoints use `ui.Width` conditions or dynamic classes.
- Do not compute page layout widths and heights in C#.
- Allow C# geometry only for algorithmic layout such as radial nodes, drag
  previews, and text measurement.

## Migrated pages

- Home, credits, the main menu overlay, the pause scrim, and every modal.
- Select save, the crew manifest, the galaxy form, and the multiplayer page,
  each with legacy parity goldens at three window sizes. The multiplayer
  goldens cover both the empty favorites list and a list with saved servers.
- The colonist form, with pinned snapshots at three window sizes. The legacy
  page overflowed its own page and clipped the footer, so it has no usable
  legacy golden. Its body preview and info box depend on text measurement that
  the stub engine does not wrap, so the snapshots gate regressions instead.
- The version watermark, crosshair, nameplates, and debug stat lines, with
  snapshots at three window sizes. `DebugOverlayRenderer` stays the C# host
  because it interleaves imperative debug sections.

## Migration phases

1. Baseline harness, golden images, command snapshots, design guide.
2. Theme files, foundation components, text input, gallery, playtest pilot.
3. Shared chrome and simple pages and modals.
4. Main menu data pages.
5. Settings.
6. HUD and overlays.
7. Chat, inventory, and radial selectors.
8. Delete old theme, metrics, widgets, and layout math.

Each phase ends with build, tests, visual baselines, and a docs pass.

## Acceptance gates

- No `UiTheme` or `UiMetrics` use in migrated page and layer code.
- No page layout math in migrated code.
- All static UI copy uses `L` keys from `.rml`.
- Context types contain no localized strings or colors.
- Component gallery goldens pass.
- Page goldens pass at `800x600`, `1280x720`, and `1920x1080`.
- Existing behavior tests pass.
- The design guide is current.

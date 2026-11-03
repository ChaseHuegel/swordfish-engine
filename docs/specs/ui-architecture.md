# UI architecture

One subject: the roles of a page or layer, its view, and its render state. This
doc defines the boundary and the rules for code on each side.

Status: current. The code is the source of truth. The migration work is in
[ui-architecture-plan](ui-architecture-plan.md).

## Pattern

Wayward Beyond UI uses an immediate-mode passive view:

- A page or layer builds a render-state record.
- A generated view draws that record.
- The view raises widget signals.
- The controller handles the signals.

This is not MVVM. There is no binding and no stateful view model. The closest
named forms are MVP with a passive view and the Humble Object pattern. The
immediate-mode loop rebuilds the state record each frame.

Do not call this pattern MVVM in code or docs.

## Roles

| Role | Test | Owns | Must not own |
|---|---|---|---|
| Controller | Holds a service or a lifecycle | Services, navigation, persistence, async work, domain rules, sounds | Layout, geometry, chrome copy |
| Presenter | Reads data and builds state | Selection, ordering, state assembly, its view instance | Services when it only presents, chrome formatting |
| View | Needs state and localization | Layout, geometry, formatting, animation, breakpoints, chrome labels | Services, domain rules |

A menu page is a controller and a presenter. A HUD layer is a presenter and
sometimes a controller. Do not split a layer into a layer and a presenter.

## Rules

1. The controller is the only place that touches a service. It does not use
   `UIBuilder` or `ElementBounds`.
2. The view is the only place that formats chrome. If a formatter needs a
   service, the result is content and stays in state.
3. Render state carries data and keys. It carries no chrome strings, no pixel
   positions, no colors, and no fonts. Animation travels as elapsed time or
   progress.
4. Events are semantic. Widget signals are allowed. Raw pixel input stops at
   the view or the widget and becomes a value.

A page never calls a view formatting method. Formatting flows one way: state to
view.

## Content and chrome

- **Chrome** is UI copy. Labels, buttons, headings, and status text travel as
  keys. The view calls `L`.
- **Content** is data shown to the player. Chat text, item names, server names,
  and load-screen hints may travel as strings.

This line decides what moves. Do not move content into the view.

## Events

Widget-shaped signals are allowed. `OnChoice(id, index)`, `OnStep(id, delta)`,
and `OnSelectTab(index)` report what the widget did.

Raw pixel input is not allowed. The slider reports a ratio, not a cursor x and
a track width. The view or the widget converts the pixels.

## Layers

A layer is a presenter for its own view. It follows the same rules. It gets no
formatting exception.

- Chrome follows rule 2.
- Content stays in state.
- Geometry, animation, and breakpoint choice move to the view.
- A selection that needs a service stays in the layer and produces content. An
  example is picking a localized load hint.

The per-layer decisions are in [ui-architecture-plan](ui-architecture-plan.md).

## Component contexts

A component context is view-internal. `SettingsRowsState` may carry
localization and callbacks. A page render state may not.

## Review checklist

- State carries data and keys. It carries no `L(...)` result for chrome.
- No pixel geometry sits in a controller or a page state.
- The page does not call a view formatter.
- The view resolves chrome.
- A service appears only in a controller.
- Events carry values, not pixels.
- A layer keeps content and moves chrome, geometry, and animation.

## Related docs

- [UI architecture plan](ui-architecture-plan.md)
- [UI design system](ui-design-system.md)
- [UI theme](ui-theme.md)
- [Reef markup](reef-markup.md)
- [Code review](code-review.md)

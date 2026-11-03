# UI architecture plan

One subject: the work to align pages and layers with the controller, view, and
render-state rules. The contract is [ui-architecture](ui-architecture.md).

Status: closed. All ten items are done. The closed review findings are in
[ui-remediation-plan](ui-remediation-plan.md). This plan is the follow-up.

## Why

The current split is mixed. A page or layer holds business logic, and its view
code-behind forwards events and formats copy. Several view concerns sit in the
page classes: pixel geometry, animation, breakpoints, and chrome formatting.
Some view events mirror the controller one to one, so the boundary decouples
less than it appears.

The fix is not a merge. The fix is a stated boundary and a pass over the leaks.

## Decisions

Settled with the owner before the work started. Do not reopen without a reason.

| Topic | Decision |
|---|---|
| Content and chrome | Content strings may stay in state. Chrome travels as keys and the view resolves it. |
| Layers | Layers follow the same rule. No layer exception. |
| Per-frame formatting | Accepted. Do not add a formatting cache now. |
| Tracker | This new plan. The review plan stays closed. |
| Pattern name | Immediate-mode passive view. Do not call it MVVM. |

## Progress

| Group | Items | Done | Open |
|---|---|---|---|
| Boundary, geometry, and events | 3 | 3 | 0 |
| Screen state | 4 | 4 | 0 |
| Audit and layers | 2 | 2 | 0 |
| Event cleanup | 1 | 1 | 0 |
| **Total** | **10** | **10** | **0** |

## Boundary, geometry, and events

- [x] **ARCH-1 — Write the boundary contract and this plan**
  - Finding: no doc states the roles. Review comments use "MVVM" loosely, and
    the review checklist has no boundary rule.
  - Fix: add `ui-architecture.md` and this plan. Link both in the doc index.
    Add the boundary bullets to the UI section of `code-review.md`. Point
    `ui-design-system.md` and the closed review plan at the contract.
  - Where: `docs/specs/ui-architecture.md`,
    `docs/specs/ui-architecture-plan.md`, `docs/README.md`,
    `docs/specs/code-review.md`, `docs/specs/ui-design-system.md`,
    `docs/specs/ui-remediation-plan.md`.
  - Verify: the docs pass. Every new link resolves.
  - Done: `a695e5db` — the contract and this plan are in place, the index and
    review checklist link them, and the closed review plan points here.

- [x] **ARCH-2 — Report a slider ratio instead of pixels**
  - Finding: `Slider.rml` passes the cursor x and the track width to
    `OnDrag`. `SettingsPage.OnSliderDrag` then clamps them into a ratio. Raw
    geometry crosses the boundary.
  - Fix: change `SliderParameters.OnDrag` to `Action<string, float>`. Add a
    `Slider` companion with `Ratio(int cursorX, int trackWidth)`. The page takes
    the ratio. `SettingsRowsState.OnDrag` takes the ratio too.
  - Where: `WaywardBeyond.Client/UI/Components/Slider.rml`,
    `SliderParameters.cs`, a new `Slider.cs`,
    `WaywardBeyond.Client/UI/Settings/SettingsRowsState.cs`,
    `WaywardBeyond.Client/UI/Layers/Menus/SettingsPage.cs`.
  - Verify: settings render and interaction tests. A new test proves that a
    click at a known x produces the expected ratio. The goldens are unchanged.
  - Done: `28658eca` — `OnDrag` takes a ratio. The conversion lives on
    `SliderParameters`, because a local component inlines into its caller and
    a companion helper is not in the caller's expression scope. The slider
    test asserts the ratio at the track center.

- [x] **ARCH-3 — Move the bar divider geometry into the view**
  - Finding: `Bars` owns `INNER_WIDTH`, `SEGMENT_GAP`, and the divider math for
    a 488-pixel track. That is view geometry in a layer.
  - Fix: `NotificationBarState` carries the segment count. The `BarsView`
    companion owns the constants and computes the positions, cached by count
    so the frame allocates nothing.
  - Where: `WaywardBeyond.Client/UI/Layers/Bars.cs`,
    `WaywardBeyond.Client/UI/Layers/BarsView.cs`,
    `WaywardBeyond.Client/UI/Layers/BarsView.rml`,
    `Swordfish.Tests/OverlayViewTests.cs`.
  - Verify: the notification-bar goldens. The test stops passing explicit
    positions and passes a count. Review the resulting positions.
  - Done: `bf451b1e` — the state carries the segment count. The view companion
    owns the width, the gap, and the cache. The centered formula moves each
    divider one pixel left, and only the three bar goldens change.

## Screen state

- [x] **ARCH-4 — Carry raw save values and format in the view**
  - Finding: `SelectSavePage.BuildState` calls `SelectSaveView.ModeTag`,
    `LastPlayedLine`, and `TimePlayedText`, then stores the text in
    `SaveRowState` and `SaveDetailState`. The page depends on the view's
    formatting API, and state carries localized copy.
  - Fix: state carries `GameMode`, `long LastPlayedMs`, and `long AgeMs`. The
    view formats through `ModeTag`, `LastPlayedText`, and `TimePlayedText` in
    the markup. The page stops calling view formatters.
  - Where: `WaywardBeyond.Client/UI/Layers/Menus/Main/SelectSaveState.cs`,
    `SelectSavePage.cs`, `SelectSaveView.rml`,
    `Swordfish.Tests/SelectSaveViewTests.cs`.
  - Verify: the select-save goldens. Add a never-played case and a creative
    case.
  - Done: `e5138e3c` — the state carries `GameMode` and the millisecond values.
    The row keeps the upper-case tag and the detail keeps sentence case through
    a new `ModeName` helper. The goldens are unchanged.

- [x] **ARCH-5 — Carry keys and numbers on the crew manifest**
  - Finding: `SelectCharacterPage.BuildState` calls `LevelLine`, `Labeled`,
    `LastPlayedText`, and `TimePlayedText`. `StatRowState` carries localized
    labels. `ShowAssignment` computes `windowHeight >= 720` in the page, an
    unnamed breakpoint.
  - Fix: `StatRowState` carries `LabelKey`. The detail carries the level, the
    species, the timestamps, and the assignment as data. The view formats and
    resolves the labels. The assignment visibility becomes a view condition
    with a named threshold.
  - Where: `WaywardBeyond.Client/UI/Layers/Menus/Main/SelectCharacterState.cs`,
    `SelectCharacterPage.cs`, `SelectCharacterView.rml`,
    `Swordfish.Tests/SelectCharacterViewTests.cs`.
  - Verify: the select-character goldens for the three sizes, plus a short
    window that hides the assignment.
  - Done: `e3a1f762` — the state carries the level, the species, the
    timestamps, stat label keys, and an optional assignment name. The view
    formats the lines and resolves the labels. `UsesTallLayout` replaces the
    raw 720 check. The goldens are unchanged.

- [x] **ARCH-6 — Carry raw new-save values and animate in the view**
  - Finding: `NewSavePage` calls `DisplayName`, `ModeLine`, and `Labeled`, and
    it computes the create-log fade and the cursor blink. Those are
    presentation and animation.
  - Fix: state carries the name, the seed, the mode, the create elapsed time,
    and each line's age. The view builds the display lines and computes the
    fade and the blink.
  - Where: `WaywardBeyond.Client/UI/Layers/Menus/Main/NewSaveState.cs`,
    `NewSavePage.cs`, `NewSaveView.rml`,
    `Swordfish.Tests/NewSaveViewTests.cs`.
  - Verify: the new-save ready and creating goldens.
  - Done: `66b3d038` — the state carries the raw name and seed, a status key,
    the line ages, and the create elapsed time. The view builds the lines, the
    fade, and the caret blink. The ready and creating goldens are unchanged.

- [x] **ARCH-7 — Format the multiplayer server labels in the view**
  - Finding: `MultiplayerPage.BuildState` builds `host:port` per row and picks
    the banner label between the saved name and the address.
  - Fix: state carries the host, the port, and the saved name. The view builds
    the address and the banner.
  - Where: `WaywardBeyond.Client/UI/Layers/Menus/Main/MultiplayerState.cs`,
    `MultiplayerPage.cs`, `MultiplayerView.rml`,
    `Swordfish.Tests/MultiplayerViewTests.cs`.
  - Verify: the multiplayer goldens.
  - Done: `7a580c8d` — the state carries the host, the port, and the saved
    name. The view builds the address and the banner. The goldens are
    unchanged.

## Audit and layers

- [x] **ARCH-8 — Audit page state for chrome**
  - Finding: SYS-2 removed most localized state, but some screens still carry
    chrome. The crew manifest was the known case and ARCH-5 covers it. The
    audit catches the rest and keeps SYS-2 from reopening.
  - Fix: read every page state record and every `BuildState` against the
    contract. Move chrome to keys. Record the result in this plan.
  - Where: `WaywardBeyond.Client/UI/Layers/Menus/**/*State.cs` and their pages.
  - Verify: a grep finds no `GetRequired` or `GetString` call that feeds a page
    state chrome field. The screen tests pass.
  - Depends on: ARCH-4, ARCH-5, ARCH-6, ARCH-7.
  - Done: `984373fc` — the menu and modal states carry keys and data. The
    confirm button labels were the last chrome and now travel as keys. Dialog
    titles and bodies stay as caller copy. The audit found no other page
    chrome that is not content.

- [x] **ARCH-9 — Apply the layer disposition**
  - Finding: layers mix content, chrome, geometry, and animation. The contract
    has no per-layer record.
  - Fix: apply the disposition from `ui-architecture.md`.
    - `LoadScreen`: fades and signal position become view animation. Status and
      hint stay as content.
    - `OrientationSelector` and `ShapeSelector`: the radial label becomes a key
      and the view resolves it.
    - `Inventory`, `ControlHints`, `ChatLayer`, `Hotbar`, `NameplateUILayer`,
      `Actions`, `Indicator`, and the notification layers: no change, because
      the data is content or the view already owns the chrome.
  - Where: `WaywardBeyond.Client/UI/Layers/LoadScreen.cs`,
    `LoadScreenView.cs`, `LoadScreenView.rml`, `OrientationSelector.cs`,
    `ShapeSelector.cs`, `RadialMenuView.rml`.
  - Verify: the load-screen, radial-menu, and control-hint goldens.
  - Depends on: ARCH-3.
  - Done: `a9a1b75e` — the load screen carries elapsed times and the view
    computes the fades and the signal position. The radial state carries a
    label key and the view resolves it. The other layers are recorded as no
    change. The goldens are unchanged.

## Event cleanup

- [x] **ARCH-10 — Bind each selector to its own step callback**
  - Finding: `NewCharacterView.OnStep` compares the control id `"species"` and
    routes every other id to gender. The id is duplicated in the markup and in
    C#. A future selector would silently step gender.
  - Fix: bind the species and gender steppers to `OnSpeciesStep` and
    `OnGenderStep`. Each partial method forwards its delta to the existing
    action. Remove the id comparison.
  - Where: `WaywardBeyond.Client/UI/Layers/Menus/Main/NewCharacterView.rml`,
    `NewCharacterView.cs`, `Swordfish.Tests/NewCharacterViewTests.cs`.
  - Verify: a view click test proves each stepper raises its own action. The
    colonist goldens are unchanged.
  - Done: `b3c20e69` — the species and gender steppers bind to separate
    methods. Two click tests prove each raises only its own action.

## Notes

These are recorded facts. They are not work items.

| Note | Detail |
|---|---|
| Component contexts | A component context such as `SettingsRowsState` may carry localization and callbacks. The rule applies to page state. |
| Widget events | Widget-shaped events stay. Only raw pixel input changes. |
| Per-frame formatting | Formatting in the view runs every frame. Accepted. If a list grows, cache in the view companion, never in the page. |
| Layer content | Load hints, chat text, item names, and server names are content. They stay in state. |
| SYS-2 boundary | Stepper values with a unit stay formatted. One key cannot express a number and a unit. |
| Method-group callbacks | A method group passed through a component parameter does not get a generated partial declaration. Implement it as a public method. |
| Dialog copy | A dialog title or body is caller copy. The dialog button labels are chrome and travel as keys. |

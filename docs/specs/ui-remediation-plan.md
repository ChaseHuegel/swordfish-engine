# UI remediation plan

One subject: the open fixes from the review of the reef markup branch, the
agreed solution for each, and where it applies.

Status: closed. All 67 items are done. The boundary follow-up is in
[ui-architecture-plan](ui-architecture-plan.md).

## Review baseline

The review ran at `3540e105` on `reef_markup_code_generation`, against
[code-review](code-review.md), [ui-design-system](ui-design-system.md),
[ui-design-system-migration-plan](ui-design-system-migration-plan.md),
[reef-markup](reef-markup.md), [reef-markup-blueprint](reef-markup-blueprint.md),
and [reef-markup-implementation-plan](reef-markup-implementation-plan.md).

Two entries are already fixed:

- `ce68b98f` Add dashed border strokes to Reef.
- `40e5973c` Draw the dashed rules with borders instead of child elements.

`git diff 3540e105..HEAD` shows the completed work.

Baseline gates at review time: build clean, all test projects green. The
two network tests in the Notes table are timing-sensitive and occasionally fail
under full suite load. They pass alone and on a rerun.

## How to use this document

- Every actionable finding is one checkbox. Tick it when the fix lands.
- Add `Done: <commit> — <one line>` under the item when you tick it.
- Update the Progress table in the same change.
- Keep the field set. Do not drop the Finding line when the item is done. The
  record is the point.
- Keep items in their group. Add new findings with the next free id in the
  group.
- A decision that needs a person is marked **Decision needed**. Do not guess.
- A settled item carries a `Decided:` line and appears in Recorded decisions. Do
  not reopen it without a stated reason.

## Rules that apply to every item

- Engine work commits alone, first, and stands green. Game work follows in its
  own commit. See [development](../development.md).
- A change is one subject. Do not fold two items into one commit.
- Every item ends with the gates in the appendix and a docs pass.
- A page that changes pixels re-baselines its goldens in the same commit, with
  a note in the commit body. Never hand-edit a golden.
- `UI_SNAPSHOT_UPDATE=1 dotnet test Swordfish.Tests` refreshes snapshot and
  legacy page goldens that differ.
- Do not add a `TODO` or a `FIXME`. This document is the tracker.
- The prose in this file and in the docs uses Simplified Technical English:
  active voice, short sentences, no semicolons, no contractions.

## Recorded decisions

Settled with the owner while finalizing this plan. Do not reopen without a
reason.

| Item | Decision |
|---|---|
| SYS-5 | Emit the palette from `Theme.rss`. Do not keep the hand copy. |
| CMP-14 | Delete `SelectableRow`. `Card` becomes the only list component. |
| DOC-9 | Document the zero-alpha side rule. Do not add a renderer opt-out. |
| SYS-7 | Co-locate one page stylesheet beside its view. No guard test. Component chrome stays in `Components.rss`. |

## Progress

| Group | Items | Done | Open |
|---|---|---|---|
| Completed in review follow-up | 2 | 2 | 0 |
| DEAD — dead code and stale config | 10 | 10 | 0 |
| SYS — design-system contract | 12 | 12 | 0 |
| LAY — layout and constraints | 7 | 7 | 0 |
| CMP — component library and reuse | 16 | 16 | 0 |
| ENG — engine and generator | 6 | 6 | 0 |
| PERF — render cost and allocation | 4 | 4 | 0 |
| DOC — documentation and records | 10 | 10 | 0 |
| **Total** | **67** | **67** | **0** |

## Suggested sequence

Each phase stands green on its own. Phases are ordered so later work does not
redo earlier work.

1. **Phase 1 — Delete dead weight.** DEAD-1 to DEAD-10. No behaviour change.
   This makes the design system honest before it changes.
2. **Phase 2 — Make the design system true.** SYS-1 to SYS-12, CMP-3 to CMP-6,
   CMP-12 to CMP-15. Widgets become markup. Tokens become the single source.
   Existing components get adopted. SYS-5 lands as an engine pair: the palette
   generator feature first, then the game switch.
3. **Phase 3 — Build the missing components.** CMP-1, CMP-2, CMP-7 to CMP-11,
   CMP-16. Icon and IconButton first, because the pages use them.
4. **Phase 4 — Constrain the layouts.** LAY-1 to LAY-7. Depends on CMP-7 and
   CMP-9 for the duplicated page bodies.
5. **Phase 5 — Engine and generator hygiene.** ENG-1 to ENG-4, then PERF-1 to
   PERF-4. These are engine commits.
6. **Phase 6 — Records and docs.** DOC-1 to DOC-10. Run the final docs pass and
   the review gate.

---

## Completed in the review follow-up

- [x] **TWO-1 — Replace the dotted divider component with a paint-time dash**
  - Finding: `DottedDivider` emitted 80 elements per rule and took a
    hand-counted `param:count` that only matched one container width. Three call
    sites used 44, 44, and 80. That is 168 commands and 168 draw calls per
    frame. `ReefRenderer` is unbatched.
  - Fix: `BorderStyle` gained `LineStyle`, `Dash`, and `Gap`. `BorderDash`
    resolves the pattern and walks segments. Both renderers paint dashes into
    the command's existing rect list. The border clamp now works per axis, so a
    one-pixel rule can carry a one-pixel bottom border. Markup gained
    `border-style`, `border-dash`, and the `solid`, `dashed`, and `dotted`
    keywords inside the border shorthands. The three call sites use
    `.wb-divider-dashed`. The component and its parameter type are deleted.
  - Where: `Reef/UI/BorderStyle.cs`, `Reef/UI/BorderDash.cs`,
    `Reef/UI/BorderLineStyle.cs`, `Reef/PixelRenderer.cs`,
    `Swordfish/Graphics/SilkNET/OpenGL/Renderers/ReefRenderer.cs`,
    `Reef.Generator/`, `WaywardBeyond.Client/UI/Components/Components.rss`.
  - Verify: `Reef.Tests/BorderDashTests` pins the legacy coordinates.
    `InventoryViewTests.DashedDividerCarriesTheSharedRulePattern` pins the
    class. Six generator tests cover parsing, emission, and diagnostics.
  - Done: `ce68b98f`, `40e5973c`.

- [x] **TWO-2 — Give item slots one size source**
  - Finding: `ItemSlot.rml` set 48 by 48 inline, `InventoryView.rml` repeated
    the same pair twice, and `HotbarView` silently relied on the component
    default.
  - Fix: `--wb-slot-size: 48px` in `Theme.rss`, `width` and `height` on
    `.wb-slot` from `var(--wb-slot-size)`, and all three call sites drop their
    copy. Caller constraints still win, so a page can size a slot when needed.
  - Where: `WaywardBeyond.Client/UI/Theme/Theme.rss`,
    `WaywardBeyond.Client/UI/Components/Components.rss`,
    `WaywardBeyond.Client/UI/Components/ItemSlot.rml`,
    `WaywardBeyond.Client/UI/Layers/InventoryView.rml`.
  - Verify: the inventory and hotbar snapshot rects are unchanged at all three
    sizes.
  - Done: `40e5973c`.

---

## DEAD — dead code and stale config

- [x] **DEAD-1 — Delete the legacy action button widget**
  - Finding: `WaywardBeyond.Client/UI/Widgets/Widgets.ActionButton.cs` has no
    call sites. It holds ten `BUTTON_*` colors at `:11-21` and re-implements the
    shared `Button` component. `Widgets.Slider` still calls it for the minus and
    plus buttons, so the palette stays live.
  - Fix: delete `Widgets.ActionButton.cs` and `ActionButtonOptions`. Move the
    slider arrows to `IconButton` (CMP-2) once it exists. If the slider is
    rebuilt as markup (SYS-1) before CMP-2, delete the widget first and let the
    slider draw its own arrows.
  - Where: `WaywardBeyond.Client/UI/Widgets/Widgets.ActionButton.cs`,
    `WaywardBeyond.Client/UI/Widgets/Widgets.Slider.cs`.
  - Verify: build. Slider interaction tests stay green. No `ActionButton` symbol
    remains (`grep -rn ActionButton`).
  - Depends on: SYS-1 or CMP-2 for the slider arrows.
  - Done: `2de995df` — the widget and its options type are gone. The slider
    steps use the shared button classes, so no hand-written button palette is
    left.

- [x] **DEAD-2 — Delete the dead widget overloads and the helper that only serves them**
  - Finding: `Widgets.Slider.cs:21` and `Widgets.Toggle.cs:24` are `IntRect`
    overloads. Nothing calls them except each other's forwarding. They are the
    only users of `Widgets.Layout.cs:12 RectConstraints`.
  - Fix: delete both `IntRect` overloads and `RectConstraints`. Keep
    `ClipToBounds` and `Row`, which the widgets use.
  - Where: `WaywardBeyond.Client/UI/Widgets/Widgets.Slider.cs`,
    `WaywardBeyond.Client/UI/Widgets/Widgets.Toggle.cs`,
    `WaywardBeyond.Client/UI/Widgets/Widgets.Layout.cs`.
  - Verify: build, `Swordfish.Tests` slider and settings tests stay green.
  - Done: `81b3b130` — the overloads and `RectConstraints` are gone.

- [x] **DEAD-3 — Remove the stale widget entries from the client project file**
  - Finding: `WaywardBeyond.Client.csproj` has 21 `<Compile Update>` entries for
    `Widgets.*.cs`. Sixteen point at files deleted in `c78ea3a7`. All set
    `<DependentUpon>Widgets.cs</DependentUpon>`, and `Widgets.cs` does not exist.
  - Fix: keep entries only for the files that remain. Remove the rest.
  - Where: `WaywardBeyond.Client/WaywardBeyond.Client.csproj`.
  - Verify: build. `ls WaywardBeyond.Client/UI/Widgets` matches the entries.
  - Done: `9dbda647` — the group is gone, and the build lost eighty-nine
    warnings.

- [x] **DEAD-4 — Remove the unused settings reset label**
  - Finding: `SettingsShellView.cs:19` declares `ResetLabel`.
    `SettingsPage.cs:165` assigns it every frame. No markup reads it. The reset
    rows hardcode `@L('ui.settings.action.reset')` at `SettingsShellView.rml:149`
    and `:268`.
  - Fix: delete the property and the per-frame localization lookup that fills it.
  - Where: `WaywardBeyond.Client/UI/Layers/Menus/SettingsShellView.cs`,
    `WaywardBeyond.Client/UI/Layers/Menus/SettingsPage.cs`.
  - Verify: build. Settings snapshot unchanged.
  - Done: `be351f83` — the property and its per-frame lookup are gone.

- [x] **DEAD-5 — Remove the unused multiplayer label helper and audit the repeated localization field**
  - Finding: `MultiplayerView.cs:114` `Label` has no callers. It uses
    `GetRequired`, while the base `UiViewBase.L` at `UiViewBase.cs:9` returns the
    key on a miss. Two contracts for one job invite drift. Sixteen view and page
    files also store `_localization` over the base field.
  - Fix: delete `Label`. Then audit the sixteen files. Keep the field only where
    the class uses a member the base class does not expose, such as
    `GetLongString`. Document the two contracts in `ui-copy` if both stay.
  - Where: `WaywardBeyond.Client/UI/Layers/Menus/Main/MultiplayerView.cs`,
    `WaywardBeyond.Client/UI/UiViewBase.cs`, and the files listed by
    `grep -rln 'private readonly ILocalization _localization'`.
  - Verify: build. Page snapshots unchanged.
  - Done: `d3816c23` — both helpers are gone. The audit also cleared the unused
    fields in `MultiplayerView` and `NewSaveView`. `NewCharacterView` had the
    same dead helper.

- [x] **DEAD-6 — Delete the dead style rules**
  - Finding: twelve rules have no reference in any `.rml`, `.cs`, or
    `--extends`: `.wb-badge` (`Components.rss:658`), `.wb-card-title` (`:555`),
    `.wb-card-meta` (`:588`), `.wb-footer-status` and its three variants
    (`:638`, `:646`, `:650`, `:654`), `.wb-icon-large` (`:136`),
    `.wb-info-box-title` (`:187`), `.wb-info-box-body` (`:193`),
    `.wb-panel-title` (`:207`), and `.wb-text-tiny` (`:81`).
  - Fix: delete the rules. Keep `InfoBox`'s title slot working by using an
    existing text class when CMP-4 adopts it.
  - Where: `WaywardBeyond.Client/UI/Components/Components.rss`.
  - Verify: build. `grep` for each name finds nothing. Snapshots unchanged.
  - Pairs with: DOC-10.
  - Done: `9ee25b0a` — twelve rules and two doc names are gone.

- [x] **DEAD-7 — Delete the unused localization keys**
  - Finding: `ui.label.character.active` and `ui.label.character.none`
    (`assets/lang/en/ui.toml:79-80`) are referenced nowhere. They are leftovers
    from the deleted hero panel.
  - Fix: delete both keys. Add a check that every key is used, if the
    localization tests can support it.
  - Where: `WaywardBeyond.Client/assets/lang/en/ui.toml`.
  - Verify: `LocalizationTests` and page snapshots pass.
  - Done: `477733ac` — both keys are gone.

- [x] **DEAD-8 — Remove the unused glyph constants**
  - Finding: `MenuGlyphs.cs:6-20` declares fifteen glyphs. Seven are never
    requested by name: `CHEVRON_LEFT`, `CHEVRON_RIGHT`, `X_MARK`, `FEEDBACK`,
    `SETTINGS`, `ROTATE` and `SHAPES` have no `Glyph('...')` call after the
    inventory and radial views moved to component-specific names. Verify each
    before deleting. `MenuGlyphs.Get` also returns `string.Empty` for an unknown
    name at `:53`.
  - Fix: delete the unused constants and their switch arms. CMP-1 replaces the
    string lookup with a typed name, which removes the silent empty result.
  - Where: `WaywardBeyond.Client/UI/MenuGlyphs.cs`.
  - Verify: `grep -rho "Glyph('[a-z-]*')"` matches the remaining names. Build.
  - Pairs with: CMP-1.
  - Done: `2340166f` — six constants and their switch arms are gone. `ROTATE`
    and `SHAPES` stayed because markup requests them.

- [x] **DEAD-9 — Collapse the duplicated style rules**
  - Finding: `.wb-tab-label` was declared twice with the same body. The two
    `.wb-button:disabled` blocks differed: the later one also reset the corner
    caps. `.wb-badge` set `color` twice and went with DEAD-6. The review also
    called `.wb-button-secondary` redundant. That was wrong: the two stat
    arrows in `NewCharacterView.rml:124` and `:136` applied only that class, so
    it supplied their fill and border.
  - Fix: keep one `.wb-tab-label`. Keep the `:disabled` block that resets the
    corner caps, so the cascade is unchanged. Delete `.wb-button-secondary` and
    point the two stat arrows at `wb-button`, which is what the alias extended.
    The 1280x720 character snapshot caught the regression before it landed.
  - Where: `WaywardBeyond.Client/UI/Components/Components.rss`,
    `WaywardBeyond.Client/UI/Layers/Menus/Main/NewCharacterView.rml`,
    `docs/specs/ui-design-system-migration-plan.md`.
  - Verify: button and tab snapshots unchanged.
  - Done: `9ee25b0a` — one declaration each remains, and the stat arrows use
    `wb-button`.

- [x] **DEAD-10 — Fix the malformed doc comment**
  - Finding: `SettingsShellView.cs:9` ends with `</summary></summary>`.
  - Fix: keep one closing tag.
  - Where: `WaywardBeyond.Client/UI/Layers/Menus/SettingsShellView.cs`.
  - Verify: build with XML doc warnings visible.
  - Done: `be351f83` — the tag is fixed.

---

## SYS — design-system contract

- [x] **SYS-1 — Rebuild the imperative controls as markup with thin behaviour widgets**
  - Finding: the editor allows behaviour widgets only for input mechanics. They
    must accept `Style` and `FontOptions` and must not hardcode theme values.
    Four controls break the rule:
    - `Widgets.Slider.cs` holds seven `SLIDER_*` constants (`:12-18`), fixes
      arrows at 24 pixels, and takes only `Constraints`.
    - `Widgets.Toggle.cs` holds `CHOICE_*` constants and takes only
      `Constraints`. It allocates `new int[options.Length]` and a capturing
      lambda per frame in `SettingsChoiceWidget.cs`.
    - `SettingsSliderWidget.cs` and `SettingsChoiceWidget.cs` forward only
      `Constraints`, so callers cannot style them.
    `Controls/TextInputWidget.cs:14-24` is the model to follow.
  - Fix: turn the settings choice row, the toggle, and the slider track and fill
    into markup. Keep one thin widget per control for drag and selection
    mechanics. Each widget takes `Style`, `FontOptions`, `Constraints`, and its
    state, and reads no colour of its own. Replace both settings wrapper types
    with components.
  - Where: `WaywardBeyond.Client/UI/Widgets/Widgets.Slider.cs`,
    `WaywardBeyond.Client/UI/Widgets/Widgets.Toggle.cs`,
    `WaywardBeyond.Client/UI/Settings/SettingsSliderWidget.cs`,
    `WaywardBeyond.Client/UI/Settings/SettingsChoiceWidget.cs`,
    `WaywardBeyond.Client/UI/Layers/Menus/SettingsShellView.rml`.
  - Verify: `Swordfish.Tests/SettingsControlWidgetTests`,
    `SliderInteractionTests`, `SettingsInteractionTests`. Settings snapshots
    unchanged at three sizes.
  - Done: `2de995df` — `SegmentedControl` and `Slider` replace the widgets and
    both wrapper types. The settings page routes a normalized drag position to
    the entry. The settings snapshots change in two reviewed ways: the slider
    buttons and track clip to their row, and the thumb sits at the right edge of
    the fill so it never leaves the track. `SettingsControlWidgetTests` becomes
    `SettingsControlTests`, and the slider tests assert the reported step and
    drag.

- [x] **SYS-2 — Carry localization keys in state instead of localized strings**
  - Finding: `ui-design-system.md:52-53` says state carries no localized
    strings. It also allows values and option keys. These types carry strings:
    - `SettingsRowState.Label` (`:14`), `SettingsRowState.Options` (`:17`), and
      `SettingsRowState.Display` (`:19`) hold resolved copy. `ChoiceEntryOfT.cs:34`
      and `SettingsSection.cs:13` resolve it with `GetString`.
    - `SettingsShellState.Title`, `.Status`, and `.CancelLabel` (`:6-13`) hold
      copy, and so does `SettingsCategoryEntry.Label`.
    - `MultiplayerState.EmptyMessage` (`:8`) holds copy built by
      `MultiplayerView.EmptyText`.
    - `NewCharacterState.InfoLabel` and `.InfoBody` (`:14-15`) hold copy.
  - Fix: state carries keys. Markup resolves them with `@L(row.LabelKey)` and
    `@L(option.Key)`. `OptionEntry` and the slider display follow the same shape.
    Keys may travel through the model. Localized strings may not.
  - Where: `WaywardBeyond.Client/UI/Settings/SettingsRowState.cs`,
    `SettingsShellState.cs`, `ChoiceEntryOfT.cs`, `SettingsSection.cs`,
    `ToggleEntry.cs`, `SliderEntry.cs`,
    `WaywardBeyond.Client/UI/Layers/Menus/SettingsShellView.rml`,
    `WaywardBeyond.Client/UI/Layers/Menus/Main/MultiplayerView.rml`,
    `WaywardBeyond.Client/UI/Layers/Menus/Main/NewCharacterView.rml`.
  - Verify: a test that asserts no state record holds a string field whose value
    comes from `ILocalization`. Settings, multiplayer, and character snapshots
    unchanged.
  - Pairs with: DOC-6, CMP-9.
  - Done: `7bce0259` — settings rows carry `LabelKey` and `OptionKeys`, and the
    shell carries `TitleKey`, `StatusKey`, and `CancelLabelKey`. Multiplayer
    carries `DiscoverKey` and `ErrorKey`, and the empty-text branch stays in the
    view. The colonist form carries the info and status keys plus a status
    value. `SelectorState.Label` and `NewCharacterState.InfoLabel` were never
    read, so both are deleted. Stepper values that name an option now travel as
    keys. Values that carry a unit (frame limit, view distance) stay formatted,
    because a single key cannot express the number. A render test with a
    prefixing localization proves the labels resolve in markup.

- [x] **SYS-3 — Use the font tokens**
  - Finding: `Theme.rss:2-4` defines `--wb-font-ui`, `--wb-font-icons`, and
    `--wb-font-brands`. Nothing reads them. `Components.rss` repeats
    `font-family: "Salmon Mono 9 Regular"` 65 times, plus four literal icon
    family declarations. `DefaultUIRenderer.cs:31` hardcodes the default font
    name.
  - Fix: replace every literal family with `var(--wb-font-ui)` or
    `var(--wb-font-icons)`. Keep the brands family for the two social icons. Read
    the default font from the generated palette once SYS-5 lands, or add a
    comment that names the token it mirrors.
  - Where: `WaywardBeyond.Client/UI/Components/Components.rss`,
    `WaywardBeyond.Client/UI/DefaultUIRenderer.cs`.
  - Verify: build. A grep finds no literal family in `Components.rss` and
    snapshots are unchanged.
  - Pairs with: SYS-5 for the default font name.
  - Done: `3d51143a` — the default font reads `WbPalette.FontUi`, and the
    fifty-five stylesheet families read `var(--wb-font-ui)`,
    `var(--wb-font-icons)`, or `var(--wb-font-brands)`. Snapshots unchanged.

- [x] **SYS-4 — Replace the raw colour literals with tokens**
  - Finding: `Components.rss` has eleven raw hex values. Several duplicate an
    existing token: `#2b384e` equals `--wb-border` (`:168`, `:219`), `#f2994a`
    equals `--wb-primary` (`:374`), `#ffd27d` equals `--wb-primary-bright`
    (`:729`), and `#8da0b880` is `--wb-text-muted` plus alpha (`:938`). The
    selection fill `#172336` appears four times without a token (`:545`, `:551`,
    `:727`, `:800`).
  - Fix: add the missing tokens, including a selection fill token and a
    transparent border token for `#00000000` (`:412`, `:1071`). Use the tokens
    everywhere.
  - Where: `WaywardBeyond.Client/UI/Theme/Theme.rss`,
    `WaywardBeyond.Client/UI/Components/Components.rss`.
  - Verify: a grep finds no hex literal in `Components.rss`. Snapshots
    unchanged.
  - Done: `c74e230d` — no hex literal is left in `Components.rss`.
    `--wb-selected-surface` and `--wb-text-faint` are new, and the zero border
    uses `transparent`. Snapshots unchanged.

- [x] **SYS-5 — Emit the palette from the theme stylesheet**
  - Finding: `DynamicColors.cs:9-17` copies nine tokens by value. A token change
    in `Theme.rss` silently diverges. `ui-theme.md` accepts the copy, but
    nothing enforces it. `TextInputPalette.cs:17-22` repeats six colour values
    from the same tokens.
  - Fix: two commits, engine first.
    - Engine: accept `Palette="<prefix>"` on the `Reef` root, where the prefix
      is the token family to emit, without the `--`. `Theme.rss` uses
      `Palette="wb"`. A palette document emits one static class of token
      constants in place of a `Render` method. Tokens outside the prefix are
      ignored. Strip `--<prefix>-` and PascalCase the rest: `--wb-text-muted`
      becomes `TextMuted`. A colour emits `Vector4`, a quoted value emits
      `string`, and an integer or a `px` value emits `int`. The class is
      `internal` by default, and `Access="public"` opts out. Children are an
      error. A name collision after mapping, or a value that is none of the
      three forms, reports a new diagnostic, `RF0035`, at the token. The
      document emits no `ReefComponent` attribute and no `Render` method.
    - Game: add `UI/Theme/Palette.rml` as
      `<Reef Class="WbPalette" Palette="wb" Namespace="WaywardBeyond.Client.UI.Theme" />`.
      Delete the nine `DynamicColors` constants and the six `TextInputPalette`
      entries. Keep `Fade`, `ToRichText`, `AccentRichText`, and
      `AccentBrightRichText`, which are derived. Point every caller at
      `WbPalette`, including the default font in `DefaultUIRenderer.cs:31`.
  - Where: `Reef.Generator/` (root attribute, palette emitter, diagnostics,
    `Schemas/reef.xsd`), `docs/specs/reef-markup.md`,
    `userdocs/reef/quick-reference.md`,
    `WaywardBeyond.Client/UI/Theme/Palette.rml`,
    `WaywardBeyond.Client/UI/Theme/DynamicColors.cs`,
    `WaywardBeyond.Client/UI/Controls/TextInputPalette.cs`,
    `WaywardBeyond.Client/UI/DefaultUIRenderer.cs`, and the six views that call
    `DynamicColors`: `LoadScreenView`, `ChatView`, `NewSaveView`, `ActionsView`,
    `BarsView`, and `NotificationView`.
  - Verify: generator tests for emission, naming, the three value kinds, child
    rejection, and the collision diagnostic. A game test asserts one member per
    `:root` token, so a renamed token fails the build. Every snapshot is
    unchanged.
  - Decided: emit the palette. A generated palette removes the copy instead of
    guarding it.
  - Done: `5274ed27` (engine) and `bb1cdce3` (game). `Palette.rml` generates
    `WbPalette`; the nine `DynamicColors` constants and the six
    `TextInputPalette` values are gone, and the default font reads
    `WbPalette.FontUi`. `Swordfish.Tests/PaletteTests` fails when the stylesheet
    gains a token the palette misses.

- [x] **SYS-6 — Collapse the duplicate class pairs**
  - Finding: `.wb-chat-scrollbar` and `.wb-chat-scrollbar-thumb` repeat
    `.wb-scrollbar` and `.wb-scrollbar-thumb`. `.wb-inventory-footer-*` repeats
    the `.wb-footer-*` idea with three near-identical rules.
  - Fix: extend the shared class with `--extends` and override only what
    differs. Delete the copies.
  - Where: `WaywardBeyond.Client/UI/Components/Components.rss`.
  - Verify: inventory and chat snapshots unchanged.
  - Done: `bb782a8c` — the chat scrollbar uses the shared classes, and the three
    inventory footer labels share one text base. `.wb-chat-panel` stays, because
    it adds a border and a different cap colour. The footer-status half of this
    finding was already resolved by DEAD-6.

- [x] **SYS-7 — Move page-specific classes out of the shared stylesheet**
  - Finding: `Components.rss` holds page-specific rules: `.wb-modal-playtest`,
    `.wb-modal-compact`, `.wb-modal-form`, `.wb-modal-feedback`,
    `.wb-modal-connecting` (`:223-267`), `.wb-playtest-body`,
    `.wb-playtest-label`, `.wb-button-playtest`, `.wb-playtest-icon-button`, and
    the settings, chat, and inventory blocks. The design rules ask for semantic
    classes, not page-specific ones.
  - Fix: keep the shared chrome in `Components.rss`. Move per-page sizes to the
    page's own `.rss` file or to a caller constraint at the call site. Use a
    `param:size` on `Modal` (CMP-15) instead of five size classes.
  - Where: `WaywardBeyond.Client/UI/Components/Components.rss`,
    `WaywardBeyond.Client/UI/Layers/Menus/**/*.rml`.
  - Verify: modal and page snapshots unchanged.
  - Done: `8459c278` — the chat, inventory, playtest, credits, and settings
    tray rules live beside their views. The tray classes carry the
    `wb-settings-tray-` prefix. The four settings text roles are now
    `wb-section-title`, `wb-row-label`, `wb-pane-title`, and
    `wb-status-message`, so they stay shared. The five modal size classes
    moved into `Modal` through `param:size` in CMP-15, and all modal and page
    goldens are unchanged.

- [x] **SYS-8 — Remove the identical typography rules**
  - Finding: `.wb-text-label` (`:56`) and `.wb-text-body` (`:49`) declare the
    same colour, family, size, and alignment. `.wb-text-value` differs from
    `.wb-text-small` only by colour.
  - Fix: keep one body and label rule with an alias, or keep the size rule and
    let the colour class carry the tone. Update call sites.
  - Where: `WaywardBeyond.Client/UI/Components/Components.rss`.
  - Verify: snapshots unchanged.
  - Done: `bb782a8c` — `wb-text-label` is gone and its five call sites use
    `wb-text-body`. `wb-text-value` extends `wb-text-small` and changes only its
    colour. Snapshots are unchanged.

- [x] **SYS-9 — Make the component library consistent in accessibility**
  - Finding: nine of sixteen components declare `Access="internal"`
    (`Button`, `Card`, `ItemSlot`, `Panel`, `ProgressBar`, `ScrollArea`,
    `ScrollBar`, `Stepper`, `Tab`). Seven default to public (`EmptyState`,
    `HintPill`, `InfoBox`, `Keycap`, `Modal`, `Page`, `SelectableRow`), which
    leaks generated types from an internal module.
  - Fix: declare `Access="internal"` on every component. Keep public only where
    a headless test project or another assembly compiles a component, and record
    why. The count is at review time. CMP-14 deletes one and other items add
    several.
  - Where: `WaywardBeyond.Client/UI/Components/*.rml`.
  - Verify: build. `Reef.Markup.Tests` and `Swordfish.Tests` stay green.
  - Done: `8bd54784` — every component declares `Access="internal"`. No page or
    test needs a public component.

- [x] **SYS-10 — Move the notification bar styling into classes**
  - Finding: `BarsView.rml` has no `.wb-*` classes. It fixes `width="512"`,
    `width="488"`, and `font-size="16"`, and takes every colour from view methods.
    The track and fill duplicate `ProgressBar`.
  - Fix: build the bar from `ProgressBar` (CMP-6) and shared classes. Keep the
    fade methods only for the alpha value, which depends on lifetime.
  - Where: `WaywardBeyond.Client/UI/Layers/BarsView.rml`,
    `WaywardBeyond.Client/UI/Layers/BarsView.cs`,
    `WaywardBeyond.Client/UI/Components/Components.rss`.
  - Verify: `1280x720-notification-bars` snapshot updated in the same commit and
    reviewed.
  - Done: `da89dcf6` — `.wb-bar`, `.wb-bar-row`, `.wb-bar-text`,
    `.wb-bar-divider`, and `.wb-bar-border` own the sizes and the font. The
    track and fill come from `ProgressBar`, and its new `Content` slot holds
    the dividers, so no wrapper or second track size is needed. The track moves
    to the shared control token and its border; the four bar goldens move and
    the element count is unchanged.

- [x] **SYS-11 — Resolve the unused layout alias classes**
  - Finding: `.wb-row` (`:17`), `.wb-stack` (`:27`), and `.wb-list` (`:31`) have
    no call site and no `--extends` user. Pages write `layout="horizontal"` or
    `layout="vertical"` inline instead. The migration plan lists all three as
    supported classes, and the design rules already forbid alias components for
    the same concepts because Reef layout is the primitive.
  - Fix: delete the three classes and their entry in
    `ui-design-system-migration-plan.md`. If a class is later wanted for list
    spacing, add it with a real consumer in the same change.
  - Where: `WaywardBeyond.Client/UI/Components/Components.rss`,
    `docs/specs/ui-design-system-migration-plan.md`.
  - Verify: a grep finds no reference to the three names.
  - Done: `bb782a8c` — the three aliases and their doc entries are gone.
    `.wb-column` stays, because five documents use it.

- [x] **SYS-12 — Make the notification panel colour intentional**
  - Finding: `NotificationView.cs:15` returns
    `DynamicColors.ScrimSoft * DynamicColors.PanelInner`. `ScrimSoft` is
    `#00000080`, so the component-wise product is black at about half alpha.
    The panel colour is discarded, and the result reads as an accident.
  - Fix: pick one. Fade `PanelInner` with the lifetime alpha, or keep the black
    scrim and drop the `PanelInner` factor. A product of two colours is not a
    valid blend and must not remain. If a scrim over a panel is the intent, add
    an explicit `Overlay(base, scrim)` helper.
  - Where: `WaywardBeyond.Client/UI/NotificationView.cs`,
    `docs/specs/ui-theme.md`.
  - Verify: the notification and notification-bar snapshots at three sizes. If
    pixels move, re-baseline in this commit and record the reason. Confirm the
    final look against the legacy notification golden.
  - Done: `f1da8e12` — the panel fades `ScrimSoft` by the lifetime alpha and
    drops the `PanelInner` factor. The scrim is black and the panel color was
    discarded by the product, so the scrim is the intent. The three
    notification goldens move from alpha 0.3779 to 0.4016.

---

## LAY — layout and constraints

- [x] **LAY-1 — Size the page frame with constraints instead of viewport arithmetic**
  - Finding: `Components/Page.rml:6-7` sets `width="@(ui.Width - 120)"` and
    `height="@(ui.Height - 240)"`. Every page inherits the magic 120 and 240.
  - Fix: wrap the page in a full-bleed element with `padding="60 120"` and size
    the frame with `width="fill"`, `height="fill"`, and the existing `max-*`
    bounds. The padding reproduces the current margins exactly, so the page
    goldens should not move. Confirm at 800, 1280, and 1920 before merging.
  - Where: `WaywardBeyond.Client/UI/Components/Page.rml`.
  - Verify: all page snapshots at three sizes, and a probe inside the
    `TitleMenu` inset gives the same frame as the root.
  - Done: `a7e7f6a5` — `PageParameters` carries the bounds. The first cut used
    a full-bleed wrapper with `fill`, which inherited the `TitleMenu` host
    padding of 64 pixels at the top and 20 at the bottom. Every page rendered
    84 px shorter and 40 px narrower, and footers left the frame. `e3026fa6`
    sizes the frame from the viewport again and replaces the narrow class with
    `insetX` and `insetY`. The exact goldens lose the empty wrapper element and
    no other element moves. The direct-root page snapshots cannot detect host
    padding, so a page change must be checked inside the `TitleMenu` inset.

- [x] **LAY-2 — Route the settings and credits frames through Page**
  - Finding: `SettingsShellView.rml:12-13` repeats the page frame with different
    constants (`ui.Width - 80`, `ui.Height - 240`, min width 320).
    `CreditsView.rml:16` uses `ui.Width - 200`. Three page frames, three rules.
  - Fix: use `Page` where the frame matches. Where a page genuinely needs
    another bound, pass it as a caller constraint on `Page` and keep the
    arithmetic out of markup.
  - Where: `WaywardBeyond.Client/UI/Layers/Menus/SettingsShellView.rml`,
    `WaywardBeyond.Client/UI/Layers/Menus/Main/CreditsView.rml`.
  - Verify: settings and credits snapshots at three sizes unchanged.
  - Depends on: LAY-1.
  - Done: `1489f460` — `PageParameters` adds the min bounds and the `insetX`
    and `topInset` flags. The settings shell uses `Page` with `insetX="40"` and
    keeps its content container for the page id and the spacing. Credits drops
    its inner wrapper. The goldens move only in transparent elements.

- [x] **LAY-3 — Replace the multiplayer column arithmetic**
  - Finding: `MultiplayerView.rml:125` is a single 605-character width
    expression. The same formula appears at `:74`, `:78`, `:82`, `:119`, and
    `:123`. Each recomputes `(ui.Width > 1280 ? 1160 : ui.Width - 120) - 60` and
    applies a hand-written clamp with `-32` or `-8` fudge factors.
  - Fix: give each column a relative width with `min-width`, and let the row
    distribute the remainder. Use `fill` for the last column so the fudge
    factors disappear. If a snapshot moves, re-baseline only after review.
  - Where: `WaywardBeyond.Client/UI/Layers/Menus/Main/MultiplayerView.rml`.
  - Verify: multiplayer snapshots at three sizes. No line longer than 120
    characters in the file.
  - Done: `aa8d5319` — the header and the rows use relative widths with min
    bounds, and one column fills the remainder. The header copies the row
    inset, spacing, and edge columns. The goldens move by a few pixels because
    the fudges are gone, and the file has no overlong lines or `ui.Width`.

- [x] **LAY-4 — Collapse the duplicated wide and narrow page bodies**
  - Finding: `NewSaveView.rml` (wide `:13-85`, narrow `:87-147` and on),
    `SelectSaveView.rml`, `SelectCharacterView.rml`, and `NewCharacterView.rml`
    each render a second full body switched on `ui.Width >= 1020`. Both branches
    emit in one frame, so the duplicate needs ten `*Narrow` element ids and a
    second input binding on the same state.
  - Fix: build one body that adapts. Where a different structure is genuinely
    needed, keep the existing tab pattern and share every field, row, and card
    through a local component (CMP-3, CMP-7, CMP-9). Remove the `*Narrow` ids.
  - Where: `WaywardBeyond.Client/UI/Layers/Menus/Main/NewSaveView.rml`,
    `SelectSaveView.rml`, `SelectCharacterView.rml`, `NewCharacterView.rml`.
  - Verify: the four pages match at three sizes. A grep finds no `Narrow` id.
  - Depends on: CMP-7.
  - Done: `6f453168` — each page renders one body. `.wb-split` and
    `.wb-split-narrow` switch the row and the column, the tab strip is narrow
    only, and each panel is visible in the wide layout or on its tab. The
    width classes are third, half, and fill, with fill winning when narrow.
    The ten `*Narrow` ids are gone, and the narrow character detail now shows
    the assignment block. Two exact goldens move only in the ids.

- [x] **LAY-5 — Render the settings rows once**
  - Finding: `SettingsShellView.rml:69-153` and `:188-272` are eighty-five
    identical lines. Only the surrounding tray or tab strip differs.
  - Fix: extract the row list as a local component (CMP-9) and call it from both
    branches.
  - Where: `WaywardBeyond.Client/UI/Layers/Menus/SettingsShellView.rml`.
  - Verify: settings snapshots at three sizes unchanged.
  - Depends on: CMP-9.
  - Done: `5a040065` — both shell branches call `SettingsRows`, and the
    eighty-five duplicated lines are gone.

- [x] **LAY-6 — Name the breakpoints**
  - Finding: three magic widths are inline in markup: `ui.Width >= 1020` four
    times, `>= 1220` once (`HomeView.rml:49`), and `>= 900` once
    (`SettingsShellView.rml:23`). No token, constant, or documented scale exists.
  - Fix: define a breakpoint scale in one place. Use a shared `.rss` variable or
    one helper on `UiViewBase`, and document the scale in
    `ui-design-system.md`. One condition form per page.
  - Where: `WaywardBeyond.Client/UI/Layers/Menus/Main/*.rml`,
    `WaywardBeyond.Client/UI/Layers/Menus/SettingsShellView.rml`,
    `docs/specs/ui-design-system.md`.
  - Verify: a grep finds the three widths only at their definition.
  - Done: `4efe5dae` — `UiViewBase` owns the three widths through
    `UsesCompactLayout`, `UsesStandardLayout`, and `UsesWideLayout`. Markup
    calls the helpers, and a grep finds the numbers only in the base class.

- [x] **LAY-7 — Move the feedback field height out of the view**
  - Finding: `FeedbackView.rml:20` uses
    `height="@(DescriptionHeight(ui.Height) + 22)"`.
    `FeedbackView.cs:26` clamps `windowHeight - 460` to 160 through 320.
  - Fix: size the field with `fill` and a `max-height`, or with the modal frame
    plus padding. Keep the view free of geometry.
  - Where: `WaywardBeyond.Client/UI/Layers/Menus/Modal/FeedbackView.rml`,
    `WaywardBeyond.Client/UI/Layers/Menus/Modal/FeedbackView.cs`.
  - Verify: feedback snapshot at three sizes unchanged.
  - Done: `e8f23976` — the message field fills the modal body, and the helper
    line takes its fixed area from the page stylesheet. `DescriptionHeight` is
    gone. The visible rects are unchanged, and the goldens move only in the
    removed transparent wrapper.

---

## CMP — component library and reuse

- [x] **CMP-1 — Add an Icon component with typed glyph names**
  - Finding: twenty-seven glyph `<Text>` nodes across twelve files, with four
    different centring idioms for the same result: `width="fill" height="fill"`
    (NewSave, NewCharacter, MainMenuOverlay, Playtest), `x="8" y="0"` with a
    parent `y="6"` (`MultiplayerView.rml:132-145`), `x="10" y="2"`
    (`SelectSaveView.rml:79`, `:212`, `SelectCharacterView.rml:66`, `:197`), and
    `anchor="Center"` (`HomeView.rml:69`). Each site spells the glyph name as a
    string, and `MenuGlyphs.Get` returns an empty string for a typo.
  - Fix: add `Icon.rml` with `param:name` typed as a generated or hand-written
    `Glyph` enum, `param:tone`, and `param:size`. The component owns the icon
    font, alignment, and size classes. Replace the near-duplicate icon classes
    with one base and tone variants.
  - Where: `WaywardBeyond.Client/UI/Components/Icon.rml`,
    `WaywardBeyond.Client/UI/Components/Components.rss`,
    `WaywardBeyond.Client/UI/MenuGlyphs.cs`, and the twelve `.rml` call sites.
  - Verify: component gallery snapshot. A grep finds no `Glyph('` outside the
    enum and the helper. Icon snapshots unchanged apart from centring fixes that
    the commit documents.
  - Pairs with: DEAD-8.
  - Done: `465b9302` — the component takes a typed name, a tone, a size, and a
    brand flag. The glyph table lives on the parameter type, because a local
    component inlines into the caller and cannot own a helper method. Five
    standalone sites are converted and the snapshot streams are unchanged.

- [x] **CMP-2 — Add an IconButton component**
  - Finding: twenty-one hand-rolled `wb-icon-button` blocks with three size
    pairs (24, 32, and free) and four centring schemes. Five commits in the
    branch are centring fixes for these blocks: `62135ab2`, `39a23cd3`,
    `be6e972a`, `bbe68162`, `aa2c7f95`.
  - Fix: add `IconButton.rml` with `param:icon`, `param:tone`, `param:size`, and
    the click and disabled attributes. It wraps `Icon` and owns the hit target
    size. Replace all twenty-one sites.
  - Where: `WaywardBeyond.Client/UI/Components/IconButton.rml`,
    `Components.rss`, and the files matching `grep -rn 'wb-icon-button'`.
  - Verify: a gallery snapshot per size and state. A grep finds no raw
    `wb-icon-button` element outside the component.
  - Depends on: CMP-1.
  - Done: `2f067f55` — the component owns the frame size, glyph size, tone, and
    danger hover. All twenty-one blocks across six views are converted. The
    nudges are gone and the glyph is centred by its text box. `MenuGlyphs` and
    the `UiViewBase` glyph helper are deleted. Three legacy goldens are
    re-baselined because the nudged glyphs moved to the centre.

- [x] **CMP-3 — Adopt the Card component**
  - Finding: `Card` has no call sites. Five blocks hand-roll it:
    `MultiplayerView.rml:105`, `SelectSaveView.rml:36`, `:169`,
    `SelectCharacterView.rml:36`, `:167`. Each repeats the caret, the row, and
    the selected class.
  - Fix: use `<Card param:selected="@...">` with the `leading` slot for the
    icon and the default slot for the lines. Keep the per-row height as a caller
    constraint. After CMP-14, `Card` is the only list component.
  - Where: the five `.rml` sites above.
  - Verify: multiplayer, select-save, and select-character snapshots unchanged.
  - Done: `4699e54b` — the five sites call `Card` with `param:selected`. Each
    row stays one child in the default slot, because its column widths and
    gaps are load-bearing. `Card` owns the frame, the caret, the selected
    class, and the click target. Every page golden is unchanged.

- [x] **CMP-4 — Adopt the InfoBox component**
  - Finding: `InfoBox` has no call sites. `NewCharacterView.rml:152-153`
    hand-rolls `wb-info-box` and does not use the component's own title class.
  - Fix: use `<InfoBox>` with the `title` slot. Delete the now-unused dead rules
    from DEAD-6 if they stay unused after the move.
  - Where: `WaywardBeyond.Client/UI/Layers/Menus/Main/NewCharacterView.rml`.
  - Verify: new-character snapshots at three sizes unchanged.
  - Done: `eb207a12` — the form calls `InfoBox` with the `title` slot. The
    component owns the frame, the divider, and the spacers. The title label is
    text colored on its own, so the call site drops the extra color class. The
    three goldens move only in the clip rect, because the component clips its
    content. Rects, colors, and text are unchanged.

- [x] **CMP-5 — Adopt the EmptyState component**
  - Finding: `EmptyState` has no call sites. `SelectSaveView.rml:25-31` and
    `MultiplayerView.rml:90-96` hand-roll the empty message.
  - Fix: use `<EmptyState>` with a `Text` child. Keep the tone class as a prop or
    a caller class.
  - Where: `SelectSaveView.rml`, `MultiplayerView.rml`.
  - Verify: snapshots unchanged for the empty states.
  - Done: `72da03ef` — both sites call `EmptyState` with the text as the
    default slot. The class now sets `text-color` only, because the old fill
    painted a dim box and the font size and alignment never reached the child.
    The page goldens are unchanged, and the component golden moves the dim tone
    from the box to the text.

- [x] **CMP-6 — Adopt the ProgressBar component**
  - Finding: `ProgressBar` has no call sites. `BarsView.rml:36-50` hand-rolls the
    track, fill, and dividers with inline styles.
  - Fix: use `<ProgressBar context="@...">` and keep the segment dividers as an
    overlay in the bar row. Extend `ProgressBarState` with an optional colour.
  - Where: `WaywardBeyond.Client/UI/Layers/BarsView.rml`,
    `WaywardBeyond.Client/UI/Layers/BarsView.cs`,
    `WaywardBeyond.Client/UI/Components/ProgressBar*.cs`.
  - Verify: notification bar snapshot updated once and reviewed.
  - Pairs with: SYS-10.
  - Done: `da89dcf6` — `BarsView` calls `ProgressBar`, and `ProgressBarState`
    gained an optional tint that falls back to the accent token. The dividers
    overlay through the component's `Content` slot. The component gallery
    golden is unchanged.

- [x] **CMP-7 — Add a FormField component**
  - Finding: eleven label-and-frame field groups repeat "label above frame"
    across four views: Feedback two, AddFavorite two, NewSave four, and
    NewCharacter three. Five of them carry a dice icon button, and the narrow
    branch repeats each one. Multiplayer adds two more server fields.
  - Fix: add `FormField.rml` with a label param or a `label` slot, the frame as
    the default slot, an optional `trailing` slot, and a hint slot. Delete the
    second copy in each layout.
  - Where: `WaywardBeyond.Client/UI/Components/FormField.rml`,
    `NewSaveView.rml`, `NewCharacterView.rml`, `MultiplayerView.rml`,
    `FeedbackView.rml`, `AddFavoriteView.rml`.
  - Verify: snapshots for the five pages at three sizes.
  - Depends on: CMP-2 for the trailing button.
  - Done: `699a9fbb` — ten field groups call `FormField` with a `label` slot
    and the frame as the default slot. The component owns the four-pixel gap,
    so the spacer elements are gone. The feedback message field sits in a
    sized element, because a component call takes no height expression. The
    multiplayer connect bar keeps its inputs: its label sits to the left, not
    above. The exact goldens move only in transparent wrappers.

- [x] **CMP-8 — Add a PageFooter component**
  - Finding: six pages repeat the same footer: a back button, a flexible status
    text, and a primary action. `SelectSaveView`, `SelectCharacterView`,
    `NewSaveView`, `NewCharacterView`, `MultiplayerView.rml:201-231`, and
    `SettingsShellView.rml:279-311`.
  - Fix: add `PageFooter.rml` with `back`, `status`, and `primary` slots, or
    typed params for the two buttons plus a status slot.
  - Where: `WaywardBeyond.Client/UI/Components/PageFooter.rml` and the six call
    sites.
  - Verify: the six page snapshots at three sizes.
  - Done: `c41b38a4` — the six pages pass `back`, `status`, and `primary`
    slots. The component owns the row height, the spacing, and the flexible
    middle box. Settings keeps its 48-pixel height through a caller
    constraint. The exact goldens move only in transparent wrappers.

- [x] **CMP-9 — Add a settings row component**
  - Finding: the settings row body is duplicated twice in
    `SettingsShellView.rml` (LAY-5). Four control branches repeat the same
    label-plus-control row with the same `33.334%` and min and max width.
  - Fix: add `SettingsRows.rml` that takes the row list as context and renders
    the four branches once. Keep the label keys in state (SYS-2).
  - Where: `WaywardBeyond.Client/UI/Components/SettingsRows.rml`,
    `WaywardBeyond.Client/UI/Layers/Menus/SettingsShellView.rml`.
  - Verify: settings snapshots at three sizes.
  - Depends on: SYS-1, SYS-2.
  - Done: `5a040065` — `SettingsRows` takes `SettingsRowsState` and renders
    the five branches once. The state carries the rows, the localization, and
    the four callbacks. Both shell branches pass the content id, because the
    scroll metrics name the wide and narrow elements. The settings goldens are
    unchanged, and this also closes LAY-5.

- [x] **CMP-10 — Add a labelled value row**
  - Finding: `HomeView.rml:94-109` repeats the label and value row four times
    with a fixed 100-pixel label column.
  - Fix: add a small `StatRow` with a label key and a value slot. The design
    rules forbid a bare label alias, so give the component the row structure,
    not just the text.
  - Where: `WaywardBeyond.Client/UI/Components/StatRow.rml`,
    `WaywardBeyond.Client/UI/Layers/Menus/Main/HomeView.rml`.
  - Verify: home card snapshots.
  - Done: `499c3a4d` — the four rows call `StatRow` with `label` and `value`
    slots. The component root is the row element, so the goldens are
    unchanged.

- [x] **CMP-11 — Move the scroll metric bookkeeping into the scroll area**
  - Finding: five pages call `SetMetrics` eight times with hardcoded element ids
    that mirror `.rml` ids: `SettingsPage.cs:143-156`, `MultiplayerPage.cs:113`,
    `SelectSavePage.cs:83-94`, `SelectCharacterPage.cs:82-89`, `CreditsPage.cs:40`.
  - Fix: let `ScrollArea` measure its own viewport and content. If a two-pass
    measure is required, add one helper that takes the scroll state and an id
    prefix, and keep the ids in one place.
  - Where: `WaywardBeyond.Client/UI/Components/ScrollArea.rml`,
    `ScrollAreaState.cs`, and the five pages.
  - Verify: the five page snapshots and a scroll interaction test.
  - Done: `c852a89c` — `ScrollAreaState.Measure` takes the viewport id and
    reads the content at the same id plus `Content`. The eight lookups become
    eight one-line calls, and every content id follows the rule. Two goldens
    move in the id only.

- [x] **CMP-12 — Use the Button component in the settings reset rows**
  - Finding: `SettingsShellView.rml:141-150` and `:260-269` hand-roll
    `wb-button wb-button-danger` and its inner border.
  - Fix: use `<Button param:variant="danger">` with the label as a `Text` child.
  - Where: `WaywardBeyond.Client/UI/Layers/Menus/SettingsShellView.rml`.
  - Verify: settings snapshots unchanged.
  - Done: `3bf68fd9` — both branches call `Button` with `param:variant="danger"`.
    The component does not clip its root, where the hand-written element did,
    so `settings-shell-wide` moves in the clip rect only.

- [x] **CMP-13 — Use the Tab component in the settings tray strip**
  - Finding: `SettingsShellView.rml:163-177` hand-rolls `wb-tab` and its bar.
  - Fix: use `<Tab param:selected="@(...)">`.
  - Where: `WaywardBeyond.Client/UI/Layers/Menus/SettingsShellView.rml`.
  - Verify: settings wide and narrow snapshots unchanged.
  - Done: `21730ccd` — the narrow strip calls `Tab` with `param:selected`. The
    component reproduces the hand-written tree, and the goldens are unchanged.

- [x] **CMP-14 — Delete SelectableRow**
  - Finding: `SelectableRow.rml` has no call sites and repeats `Card.rml`
    without the caret or the selected param. Two components, one concept.
  - Fix: delete `SelectableRow.rml`. `Card` becomes the only list component
    (CMP-3). Update the two places that name it. `ui-design-system.md:126-127`
    becomes "Lists use `ForEach`. Item structure belongs in `Card`." The
    migration-plan component list drops the `SelectableRow` line. A row that
    needs no card chrome is a `layout="horizontal"` element, which the design
    rules already allow.
  - Where: `WaywardBeyond.Client/UI/Components/SelectableRow.rml`,
    `docs/specs/ui-design-system.md`,
    `docs/specs/ui-design-system-migration-plan.md`.
  - Verify: build. A grep finds no `SelectableRow`. List snapshots unchanged.
  - Decided: delete it.
  - Done: `bb782a8c` — `SelectableRow` is deleted, and the list guidance names
    `Card`.

- [x] **CMP-15 — Make the modal action row optional**
  - Finding: `Modal.rml:11` always reserves a 40-pixel actions row. Modals
    without actions, such as connecting and message, still lay it out.
  - Fix: add `parameters: ModalParameters(bool Actions = true)` and skip the row
    when false. Add a size param so SYS-7 can move the five size classes out.
  - Where: `WaywardBeyond.Client/UI/Components/Modal.rml`, a new
    `ModalParameters.cs`, and the modal call sites.
  - Verify: every modal snapshot at three sizes.
  - Done: `dd2e7b21` — `Modal` takes `param:size` with the `connecting`,
    `compact`, `form`, `feedback`, and `playtest` presets. The base preset
    class owns the full-bleed size and the center anchor, and `auto` leaves
    the size to the caller. No `Actions` flag was added: every current modal,
    including connecting and message, passes an actions slot, so the flag
    would have no caller. The five size classes are gone from the call sites,
    and every modal golden is unchanged.

- [x] **CMP-16 — Cover the component gallery against the new components**
  - Finding: the gallery and snapshots cover the current set. The design rules
    ask for normal, hover, active, focus, disabled, and selected states per
    component.
  - Fix: add gallery entries and snapshots for `Icon`, `IconButton`,
    `FormField`, `PageFooter`, `SettingsRows`, `StatRow`, and every state that
    applies. Keep one golden per state.
  - Where: `Swordfish.Tests/ComponentSnapshotTests.cs`,
    `Reef.Markup.Tests/Fixtures/GalleryView.rml`, and
    `WaywardBeyond.Client/UI/Components/`.
  - Verify: the gallery test and the new goldens.
  - Done: `0f511b44` — the gallery adds one golden per icon tone, an icon
    button and its danger state, a form field with and without a hint, the
    page footer, one golden for all five settings row branches, and the stat
    row. The engine fixture stays as it is, because the game components are in
    another assembly.

---

## ENG — engine and generator

- [x] **ENG-1 — Restore generator incrementality**
  - Finding: `ReefGenerator.cs:37-50` combines `.Collect()` for markup,
    `.Collect()` for styles, the whole `Compilation`, and the options provider.
    Any code edit re-emits every document. `RmlParseResult` and `RssFile` are
    classes with reference equality, so incremental caching misses on every run.
  - Fix: register one source output per document, feed it with a shared style
    provider that has value equality, and keep the compilation out of the
    per-document pipeline where possible. Add the incremental test the plan
    already claims.
  - Where: `Reef.Generator/ReefGenerator.cs`, `Reef.Generator/*Model.cs`,
    `Reef.Generator.Tests/`.
  - Verify: a test that edits one `.rml` and asserts only that document is
    regenerated. A test that edits one `.rss` and asserts the style output
    changes once.
  - Done: `7eb1bbfb` — each document has its own source output. The style
    catalog and the registry are upstream with value comparers, and the
    registry signature covers only referenced components, so a leaf page edit
    re-emits one document. The style catalog validates every class before its
    diagnostics are captured. Two tests replace one additional text and assert
    the modified steps.

- [x] **ENG-2 — Clean up the expression binder**
  - Finding: four defects and two naming problems.
    - `ExpressionBinder.cs:43` names an out parameter `bonus`.
    - `:1213` reads `from is ITypeSymbol`, which is always true.
    - `TryNormalize` at `:789-794` replaces `@context.` and `@ui.` in the raw
      text before literals are converted, so the same text inside a quoted
      argument is rewritten.
    - `ResolveMethod` at `:1069` skips every generic method, so a generic helper
      cannot be called from markup.
    - `ResolveMethod` returns the left operand type for arithmetic, which can
      pick the wrong overload.
    - Interface members resolve for properties only, not methods.
  - Fix: rename the parameter, drop the dead condition, tokenize before the
    prefix replacement or apply it outside literals, support generic methods
    with inference, compute the binary result type, and resolve interface
    methods. Add a test per case.
  - Where: `Reef.Generator/ExpressionBinder.cs`,
    `Reef.Generator.Tests/`.
  - Verify: new generator tests, existing emission tests unchanged.
  - Done: `40209747` — the out parameter is `bound`, and the dead type check is
    gone. Literals are converted before the prefix rewrite, which then skips
    literals. Generic methods infer their type arguments. Interface methods
    resolve through the inherited interfaces. Binary arithmetic promotes the
    two numeric types. Four tests cover the literal, the generic call, the
    inherited interface method, and the mixed addition.

- [x] **ENG-3 — Align stylesheet ordering with its documentation**
  - Finding: `StyleResolver.cs:57` sorts files by full path. `reef-markup.md`
    says declarations cascade in file discovery order. The code is
    deterministic, but the doc describes something else.
  - Fix: either document the path order as the cascade order, or keep discovery
    order in the sort. State one rule in one place.
  - Where: `Reef.Generator/StyleResolver.cs`,
    `docs/specs/reef-markup.md`.
  - Verify: a test that two files with the same class cascade in the stated
    order.
  - Done: `b4f71a9f` — the spec names the ascending full-path order that the
    resolver already sorts. The cascade test passes its files in reverse input
    order, so the path rule is what it proves.

- [x] **ENG-4 — Guard the duplicated attribute lists**
  - Finding: `MarkupVocabulary` keeps the style attribute names in both
    `COMMON_ATTRIBUTES` and `STYLE_ATTRIBUTES`, and both must stay in sync. A new
    attribute added to one list silently fails validation in the other place.
  - Fix: derive one list from the other, or add a test that asserts
    `STYLE_ATTRIBUTES` is a subset of `COMMON_ATTRIBUTES` and that every parser
    property has an attribute name.
  - Where: `Reef.Generator/MarkupVocabulary.cs`,
    `Reef.Generator.Tests/`.
  - Verify: the new test.
  - Done: `e2039bd2` — `STYLE_ATTRIBUTES` starts from `COMMON_ATTRIBUTES`, adds
    the text, layout, and size extras, and removes the non-style names. A test
    fails when a style name has no source in either list.

- [x] **ENG-6 — Keep component inlining out of the caller's expressions**
  - Finding: three scoping defects surfaced when a parameterized component
    called another component. `CloneWith` shared the variable dictionary, so
    inlining a child replaced the caller's `param` for everything after it.
    Call-site callbacks were emitted inside the child body, so they bound
    against the child's parameters. The document's own local was named `param`,
    which the child rename pass rewrote. The binder also emitted raw subtree
    text for unary, binary, conditional, index, and interpolated expressions,
    which is what the rename masked.
  - Fix: copy the variables per scope, bind call-site callbacks in the caller's
    scope, name the document local `__param`, compose those expressions from
    bound operands with explicit parentheses, and accept a `param`-derived id as
    key-bound in a loop.
  - Where: `Reef.Generator/ExpressionScope.cs`,
    `Reef.Generator/MarkupEmitter.cs`,
    `Reef.Generator/MarkupEmitterSupport.cs`,
    `Reef.Generator/ExpressionBinder.cs`,
    `Reef.Generator/DocumentEmitter.cs`, `Reef.Generator/StylePlan.cs`,
    `Reef.Generator.Tests/`.
  - Verify: a nested call keeps the caller's parameter, a call-site callback
    binds in the caller's scope, a parameter-derived loop id is accepted, a loop
    variable does not leak, and the existing generator tests pass.
  - Done: `381f2325`.

- [x] **ENG-5 — Accept a component call's loop id for its inlined root**
  - Finding: a local component inlines into the caller, so its root element has
    no `id` attribute of its own. A call site that passed a key-bound id still
    tripped `RF0010` when the component root was interactive, which it is when
    the root carries hover styling. `IconButton` hit this in every list row.
  - Fix: keep the call-site id text on `CallerPlan` and accept it when it names
    a loop item or index. A static call-site id still reports `RF0010`.
    Document the rule in `reef-markup.md` beside the existing component id
    paragraph.
  - Where: `Reef.Generator/StylePlan.cs`,
    `Reef.Generator/MarkupEmitterSupport.cs`,
    `Reef.Generator.Tests/ComponentLoopIdTests.cs`,
    `docs/specs/reef-markup.md`, `userdocs/reef/quick-reference.md`.
  - Verify: two generator tests. A key-bound call-site id compiles, and a
    static id still reports the diagnostic.
  - Done: `608aeafe`.

---

## PERF — render cost and allocation

- [x] **PERF-1 — Hold view instances instead of constructing them per frame**
  - Finding: fourteen call sites build a view inside `RenderUI`:
    `Bars.cs:63`, `Hotbar.cs:104`, `Actions.cs:52`, `LoadScreen.cs:87`,
    `ControlHints.cs:89`, `NotificationLayer.cs:42`, `ShapeSelector.cs:126`,
    `OrientationSelector.cs:130`, `CrosshairOverlay.cs:41`, `VersionWatermark.cs:16`,
    `NameplateUILayer.cs:26`, `NetworkStatsOverlay.cs:43` and `:91`, and
    `PerformanceStatsOverlay.cs:43`.
  - Fix: hold one `readonly` instance per layer. Views that own mutable state,
    such as `ChatView`, already follow this pattern.
  - Where: the fourteen sites above.
  - Verify: build. Snapshots unchanged. No `new ...View()` inside a render
    method.
  - Done: `e31bf4f6` — every layer holds one readonly view instance. The views
    are stateless, so the goldens are unchanged.

- [x] **PERF-2 — Stop rebuilding settings rows every frame**
  - Finding: `SettingsPage.RenderPage` allocates a `SettingsCategoryEntry[]`
    (`:103`) and resolves a localization key per category. `RenderRows` (`:158`)
    allocates a list and one row state per row, and resolves two keys. Every
    choice row copies its option labels twice: `ChoiceEntryOfT.cs:26` builds the
    array and `SettingsRowState.cs:45` copies it again.
  - Fix: cache the category entries and row states, and invalidate on enter, on
    draft change, and on edit. Pass the label array through without a second
    copy. Keep the localized strings out of state (SYS-2).
  - Where: `WaywardBeyond.Client/UI/Layers/Menus/SettingsPage.cs`,
    `SettingsRowState.cs`, `ChoiceEntryOfT.cs`.
  - Verify: `SettingsPageRenderTests` and `SettingsInteractionTests`. A change
    to the draft still refreshes the rows.
  - Done: `784a8172` — the category entries are built once on enter, and the
    rows rebuild only on a choice, a step, a drag, a reset, a category switch,
    an apply, or an external display refresh. The option keys already pass
    through as one array after SYS-2.

- [x] **PERF-3 — Take the bar clock from the frame**
  - Finding: `Bars.cs` calls `DateTime.Now` per frame and allocates a list and a
    bar state per visible bar, plus a divider list per bar.
  - Fix: use `ui.Time` for the fade, reuse one list, and compute dividers into
    the state without a new list per bar.
  - Where: `WaywardBeyond.Client/UI/Layers/Bars.cs`.
  - Verify: `1280x720-notification-bars` snapshot unchanged.
  - Done: `f474aea9` — the layer reuses one bar list and one divider array
    per segment count. The fade keeps the wall clock: the notification state
    records a wall-clock creation time, and `ui.Time` counts from UI start, so
    the two clocks are not interchangeable. The snapshot is unchanged.

- [x] **PERF-4 — Stop formatting debug overlays every frame**
  - Finding: `NetworkStatsOverlay` and `PerformanceStatsOverlay` build lists and
    interpolated strings per frame. This is developer-only surface.
  - Fix: keep the strings but gate the work on visibility, or build into a
    reused list. Low value. Do it only if the debug overlay cost shows up in a
    profile.
  - Where: `WaywardBeyond.Client/UI/NetworkStatsOverlay.cs`,
    `WaywardBeyond.Client/UI/PerformanceStatsOverlay.cs`.
  - Verify: F3 overlay still renders the same lines.
  - Done: `0a5f12ba` — both overlays reuse one line list. The formatted
    strings stay, because no profile shows this developer-only surface and
    the item is conditional on one.

---

## DOC — documentation and records

- [x] **DOC-1 — Finish the reef-ui Style contract**
  - Finding: `docs/specs/reef-ui.md:70-76` lists the `Style` members and omits
    `TextColor`, which the branch added. The border paragraph and the renderer
    pointers are already current from the dash work.
  - Fix: add `TextColor` and state the inheritance rule in one line.
  - Where: `docs/specs/reef-ui.md`.
  - Verify: the member list matches `Reef/UI/Style.cs`.
  - Done: `55ae6f86` — the Style member list adds `TextColor` and its inheritance rule.

- [x] **DOC-2 — Document every diagnostic and drive the test from the catalogue**
  - Finding: `DiagnosticDescriptors.cs:209` defines RF0034.
    `reef-markup.md:647` documents it. `quick-reference.md` does not.
    `Reef.Markup.Tests/DocumentationTests.cs:43` loops `1..33` and asserts
    RF0099, so a new diagnostic can never fail the test.
  - Fix: add RF0034 to the quick reference. Rewrite the test to enumerate the
    identifiers from the descriptor class or from a generated list.
  - Where: `userdocs/reef/quick-reference.md`,
    `Reef.Markup.Tests/DocumentationTests.cs`.
  - Verify: the test fails when a descriptor is added without a doc entry.
  - Done: `bc28ec6a` — the check enumerates the descriptor fields and lives in the generator test project, which can see the internal type.

- [x] **DOC-3 — Repair the issue index and the nested-input record**
  - Finding: `docs/issues/index.md:79` is a markdown table row under Research,
    while every other entry is a checkbox bullet. It lists 0095 as `Open`, while
    `0095-nested-markup-view-input.md:5` says `Resolved`. That document also
    breaks the template at `docs/specs/issues.md:18-33`: no type, no status
    bullet, no acceptance criteria, and a filename that no longer matches its
    content. `0086-incremental-voxel-rebuild.md` and
    `0086-keybinding-rebinding.md` share a number.
  - Fix: rewrite 0095 to the template with type, status, and criteria, or delete
    it if it is fully resolved and the regression tests carry it. Convert the
    row to a bullet. Renumber one of the 0086 documents and update the index.
  - Where: `docs/issues/index.md`, `docs/issues/0095-nested-markup-view-input.md`,
    `docs/issues/0086-*.md`.
  - Verify: `docs/specs/issues.md` checklist read against the file.
  - Done: `d5512551` — 0097 uses the template and a matching filename, the index lists it as a done bug, and the keybinding issue takes 0096.

- [x] **DOC-4 — Correct the implementation plan's structure and claims**
  - Finding: `docs/specs/reef-markup-implementation-plan.md:58` claims
    incremental tests that do not exist. `:64-120` draws a folder tree with
    `Common/`, `Parsing/`, `Model/`, `Analysis/`, and `Emission/` folders and
    files such as `ExpressionParser.cs` and `CSharpEmitter.cs` that are not in
    the tree. The generator is flat.
  - Fix: replace the tree with the real file list, and drop or mark the
    incremental claim until ENG-1 lands.
  - Where: `docs/specs/reef-markup-implementation-plan.md`.
  - Verify: every path in the document exists.
  - Done: `f71f2fc6` — the tree lists the real flat files, and the incremental claim is true after the per-document outputs.

- [x] **DOC-5 — Use one spelling for the text input service attribute**
  - Finding: the docs use `input-service`. `NewSaveView.rml:31`, `:52`, `:110`,
    `:131` and `ChatView.rml:114` use `inputService`. Both compile, so the
    generator accepts an undocumented spelling without a diagnostic.
  - Fix: switch the five sites to `input-service`. Decide whether a camel-case
    widget argument should warn, and add the diagnostic if it should.
  - Where: the five lines above, and `Reef.Generator/WidgetEmitter.cs` if a
    diagnostic is added.
  - Verify: build and page snapshots unchanged.
  - Done: `47fa0927` — the three inputs use `input-service`.

- [x] **DOC-6 — Align the settings authoring rule with the implementation**
  - Finding: `ui-design-system-migration-plan.md:30-32` says settings rows are
    authored explicitly in `.rml` so label keys stay designer-facing. The
    implementation is data driven: the rows are built in
    `Settings/*Category.cs`, and labels travel as localized strings.
  - Fix: state the rule the implementation follows: rows are data driven, keys
    travel through state, and `.rml` resolves them (SYS-2). Remove the
    "authored explicitly" sentence. `ui-design-system.md:40` already allows
    values and option keys.
  - Where: `docs/specs/ui-design-system-migration-plan.md`,
    `docs/specs/ui-design-system.md`.
  - Verify: the doc matches the code after SYS-2.
  - Pairs with: SYS-2.
  - Done: `c0b3566a` — the rule states that rows are data driven and carry keys through state.

- [x] **DOC-7 — Correct the theme font claim**
  - Finding: `ui-theme.md:11-13` presents `--wb-font-*` as the token source.
    Nothing reads those tokens before SYS-3, and 65 literal families live in the
    stylesheet.
  - Fix: after SYS-3, keep the claim and add a line that the stylesheet must
    reference the tokens. Before SYS-3, state that the tokens are unused.
  - Where: `docs/specs/ui-theme.md`.
  - Verify: a grep of `Components.rss` uses the tokens.
  - Pairs with: SYS-3.
  - Done: `2c8fa742` — the font tokens require `var()` in every text rule.

- [x] **DOC-8 — Document ui.Hovering with an element id**
  - Finding: `HomeView.rml:49` calls `ui.Hovering('Button_Continue')`. The
    expression surface in `reef-markup.md` lists `ui.Hovering()` without an
    argument only.
  - Fix: document the id overload, its single-frame semantics, and the rule that
    the id must name an element rendered earlier or later in the same frame.
  - Where: `docs/specs/reef-markup.md`, `userdocs/reef/quick-reference.md`.
  - Verify: the doc example compiles in a fixture.
  - Done: `e2d8ac2e` — the interaction section and the quick reference name the id query and its same-frame rule.

- [x] **DOC-9 — Document the zero-alpha side border rule**
  - Finding: a side colour with zero alpha falls back to the shared colour, so a
    deliberately transparent side cannot be expressed. The rule is implied at
    `reef-markup.md:538-539`, but the zero-alpha case is not stated.
  - Fix: state it. Replace the sentence with: "A side thickness of zero uses the
    shared thickness. A side colour with zero alpha uses the shared colour. To
    omit a side, set its thickness to zero." Add the same line to the
    `BorderStyle` paragraph in `reef-ui.md`.
  - Where: `docs/specs/reef-markup.md`, `docs/specs/reef-ui.md`.
  - Verify: the sentence matches `ResolveThickness` and `ResolveColor` in both
    renderers. Build. No snapshot change.
  - Decided: document it. No page needs a transparent side.
  - Done: `55ae6f86` — both docs state the zero-alpha fallback and the omission rule.

- [x] **DOC-10 — Drop the dead class and component claims**
  - Finding: `ui-theme.md:26-29` and the migration plan list badges, footer
    status, and `DottedDivider` as part of the system. The first two are dead
    rules and the third is deleted.
  - Fix: update both docs after DEAD-6 and CMP-14 settle the final class and
    component set.
  - Where: `docs/specs/ui-theme.md`,
    `docs/specs/ui-design-system-migration-plan.md`,
    `docs/specs/ui-design-system.md`.
  - Verify: every class and component named in the docs exists and is used, or
    is marked as a future item.
  - Pairs with: DEAD-6, CMP-14.
  - Done: `2c8fa742`, `c0b3566a` — the class and component lists match the code.

---

## Notes

These are recorded facts. They are not work items.

| Note | Detail |
|---|---|
| Flaky network tests | `TransportPolicyTests.StalledPeerIsClosedWithBacklogLimit` and `ServerRpcTests.TcpMenuCallsUseTheLanEndpointAndDenyRemoteWrites` fail occasionally under full suite load. Both pass alone and on a rerun. Both are socket timing tests. They are outside this plan. |
| Mixed engine and game commit | `3a0899e6` changed `Reef.Generator/*` and `WaywardBeyond.Client/*` in one commit. History only. Do not repeat it. |
| Thin widget wrapper | `Controls/TextInputWidget.cs` forwards to `Widgets.TextBox` with reordered arguments and defaults. Keep it as the markup-facing adapter. |
| Text input palette | Resolved by SYS-5. `TextInputPalette.Default` reads `WbPalette` members, so no colour is copied. |
| Fixed sizes are allowed | The 262 width and 498 height attributes in markup are mostly slot cells, icons, hit targets, and glyph boxes. The design rules allow those. Do not chase them. |
| Caller font overrides | `StyleOverride` carries colour, padding, radius, border, and caps, but no `FontOptions`. A component call therefore cannot restyle its text size or font. `Icon` and `IconButton` take a size parameter for this reason. Revisit if a component needs caller typography. |
| Caller clip on a component call | The `clip` attribute on a component call does not override the clip of the component root. A call that needs the root clipped must set it in the component markup. |
| Mutable form state | `FeedbackState` exposes public mutable `TextBoxState` fields. Reference state is required for `bind`. Prefer properties, but this is style only. |
| Local runtime | `Swordfish.Tests` and `WaywardBeyond.Voxels.Tests` need `DOTNET_ROLL_FORWARD=LatestMajor` on a machine without the .NET 9 runtime. CI uses .NET 9. |
| Clean observations | All `.rml` use `wb-` classes and `L` keys. No inline hex colour lives in `.rml`. Every detected `L` key exists in `ui.toml`. All test projects were green at the baseline. |

## Verification appendix

Run these before you close an item.

```bash
dotnet build
dotnet test Reef.Tests
dotnet test Reef.Generator.Tests
dotnet test Reef.Markup.Tests
dotnet test Reef.Text.Tests
DOTNET_ROLL_FORWARD=LatestMajor dotnet test Swordfish.Tests
DOTNET_ROLL_FORWARD=LatestMajor dotnet test WaywardBeyond.Voxels.Tests
```

Golden refresh:

```bash
UI_SNAPSHOT_UPDATE=1 DOTNET_ROLL_FORWARD=LatestMajor dotnet test Swordfish.Tests
```

`UI_SNAPSHOT_UPDATE=1` rewrites snapshot goldens and legacy page goldens that
differ. It never rewrites a file that already matches. Review the diff before
you commit it.

Useful checks:

```bash
# Layout math in markup
grep -rn 'ui\.Width\|ui\.Height' --include='*.rml' WaywardBeyond.Client

# Dead classes
grep -rn 'wb-' --include='*.rml' WaywardBeyond.Client | wc -l

# Literal font families in the stylesheet
grep -c 'font-family: "' WaywardBeyond.Client/UI/Components/Components.rss

# Raw colours in the stylesheet
grep -n '#[0-9a-fA-F]\{3,8\}' WaywardBeyond.Client/UI/Components/Components.rss

# Per-frame view construction
grep -rn 'new [A-Z][A-Za-z]*View()' --include='*.cs' WaywardBeyond.Client/UI

# Glyph lookups
grep -rho "Glyph('[a-z-]*')" --include='*.rml' WaywardBeyond.Client | sort | uniq -c
```

## Final review gate

Before the branch is ready:

1. Every checkbox above is ticked, or carries a note that says why it is not.
2. The Progress table matches the checkboxes.
3. The docs pass is complete and every `file:line` pointer is current.
4. The full gate in the appendix is green, with the one known flaky pair noted.
5. Self-review against [code-review](code-review.md) finds nothing blocking.

## Related docs

- [Code review](code-review.md)
- [UI design system](ui-design-system.md)
- [UI design-system migration plan](ui-design-system-migration-plan.md)
- [UI theme](ui-theme.md)
- [Reef markup](reef-markup.md)
- [Reef UI](reef-ui.md)

# Star chart

One subject: the save-screen star chart. Its component, geometry, interaction,
and styling.

## Scope

The chart is the right pane of the save screen. It replaces the telemetry panel.
Each save is a star node, and its telemetry shows in a hover card.

Everything lives beside the page in
`WaywardBeyond.Client/UI/Layers/Menus/Main/`. The page builds state, the view
builds the chart context, and the component draws it. See
[ui-architecture](ui-architecture.md) for those roles.

## Files

| File | Role |
|---|---|
| `SelectSaveView.rml:1` | Page frame, archive list, tabs, one `<StarChart>` call |
| `SelectSaveView.cs:12` | `Chart(...)` builder, formatting, geometry cache |
| `StarChart.rml:3` | Component: field, specks, nodes, card |
| `StarChart.rss:1` | `wb-star-*` classes |
| `StarChartState.cs:9` | Component context and node record |
| `StarChartLayout.cs:40` | Pure geometry |

The component sits beside its only caller, as `PlaytestNoticeView` and
`CreditsView` sit beside theirs. `UI/Components/` holds cross-page components.

## Geometry

`StarChartLayout.Compute` takes `(Guid, Seed)` sources, the selected Guid, and
the field size. It returns placements and specks. It has no UI dependency.

Nodes live in a fixed world of `1000 x 500` world units. Position, separation,
and clamping all happen in world units, so a save's coordinates never depend on
the panel size. `Projection(width, height)` fits the world into the field with a
uniform scale, `min(width / 1000, height / 500)`, and centers it. The narrow tab
therefore letterboxes instead of reshaping the constellation.

- **Base position.** `FNV1a.ComputeHash64(seed)` gives the X and Y fractions of
  the world. X spans 0.14 to 0.86, Y spans 0.18 to 0.84, so no node sits on the
  world edge.
- **Class.** `FNV1a.ComputeHash64("class:" + seed) % 4` picks white, red,
  emerald, or violet. The salt keeps class bits apart from position bits.
- **Clamp.** Centers stay 80 world units from the horizontal edges and 50 from
  the vertical edges.
- **Separation.** Saves are processed in ascending Guid order, so play order
  never moves a node. A node within 40 world units of an earlier node steps 40
  units along a direction derived from `FNV1a.ComputeHash64(guid + ":" + attempt)`,
  for at most 8 attempts.
- **Overlap.** `Overlaps` treats two 24px hit boxes as overlapping when both
  axis distances are under 24.
- **Card box.** The card sits 36px from the node, flips when it would leave the
  field, and clamps to both axes.
- **Specks.** 1px squares from a seed of the field size, capped at 120, so they
  do not shimmer.

## Labels

- Position: centered under the node, `x = nodeX - 80`, `y = nodeY + 14`. A label
  never depends on its neighbours, so adding or removing a save does not move
  another label.
- Card: fixed 310px wide and `height="fit"`, so a long seed phrase wraps and
  the card grows downward. `StarChartLayout.CARD_HEIGHT` reserves the tallest
  expected card for flip and clamp math, so the top edge never moves as text
  wraps. Rows use `StatRow param:wrap="true"`, whose height fits its content.
- Scrim: the text sits on `.wb-star-label-scrim`, a `--wb-chart-label-scrim`
  plate that hugs the text with 4px of horizontal padding. Where two labels
  overlap, the front one stays readable.
- Paint order: unselected nodes first, then the selected node, then the hovered
  node. `BuildNodes` emits the list in that order, so a selected node covers its
  unselected neighbours and a hovered node covers everything.

## Interaction

- Hover and selection are markup expressions. `ui.Hovering` is evaluated in the
  component, so no view field tracks hover. Element queries are pure reads, so
  they do not retarget the element that asks.
- The hit target is 24 by 24 pixels, centered on the node. Labels are inert, so
  a displaced label never takes hover from a node.
- The card renders inside the node loop under an `<If>`, so only the hovered
  node draws one.
- A **selected** node is `passthrough` only when another node's hit box overlaps
  it. The rest of the node keeps hover and click. `passthrough` accepts a
  literal only, so the hit target is declared twice under `<If>` and `<Else>`.
- Two overlapping nodes are supported. Three or more fall back to the archive
  list.

## Component rules

- A component cannot call `L`, so `StarChartState` carries `ILocalization`,
  `OnSelect`, and a `Text(key)` helper. `SettingsRowsState` is the precedent.
- Keep `<If>` conditions to one member access. A binary comparison bound
  `context` to the caller's type and failed with `RF0004`.
- Interactive elements inside `ForEach` need the `{node.Guid}` id form.
  Expression-built ids fail with `RF0010`.
- The diamond is a `<Text>`, not `<Icon>`. `Icon` writes `font-size` inline,
  which beats class rules, so size cannot vary by state. `StarChartState.Glyph`
  supplies the codepoint from `IconParameters`.
- The field needs an explicit `id="SelectSave.Chart"` wrapper. A call-site `id`
  is an `idPrefix`, and prefixes only reach derived ids.
- The field size comes from `GetElementBounds` on the previous frame, so the
  first frame has no nodes. `StarChartState.IsEmpty` suppresses the empty prompt
  until the field is measured.

## Styles

`StarChart.rss` holds the `wb-star-*` classes and no tokens. Tokens live in
`Theme.rss`. `.wb-star-glyph` is 12px, `-hover` 14px, and `-selected` 16px, so
the glyph grows without a transform.

## Tests

- `Swordfish.Tests/StarChartLayoutTests.cs` covers determinism, bounds,
  separation, the attempt limit, overlap, the card box, specks, and selection.
- `Swordfish.Tests/StarChartViewTests.cs` renders the component from a fixed
  state and checks names, glyphs, brackets, the empty prompt, the hover card,
  passthrough, specks, and class colors.
- `Swordfish.Tests/SelectSaveViewTests.cs` captures the whole page in two frames
  against `TestFiles/Snapshots/page-select-save-{w}x{h}.txt`.

## Related docs

- [Main menu UI](main-menu-ui.md)
- [UI architecture](ui-architecture.md)
- [UI design system](ui-design-system.md)
- [UI theme](ui-theme.md)
- [UI copy](ui-copy.md)
- [Reef markup](reef-markup.md)
- [Seed phrases](seed-phrases.md)

# Reef UI

One subject: the Reef immediate-mode element tree, layout constraints, styling,
text wrapping, clipping, and the two renderers.

## Element tree

`Reef/UIBuilder.cs` builds an element tree for one frame. Each `Element` holds
its rect, style, layout, constraints, text, texture data, ID, viewport, and
children. `UIBuilder.Build` runs three passes:

1. `FillShrinkAndWrapElements` resolves relative sizes, distributes fill space,
   shrinks oversized children, and wraps text.
2. `PositionAndRenderElements` resolves positions, anchors, clipping, and
   produces `RenderCommand` values.
3. The controller updates interaction state from the render commands in reverse
   order.

## Layout

`Reef/UI/Constraints.cs:6` holds four size constraints and four min/max bounds.
The constraint types live in `Reef/Constraints/`:

- `Fixed` uses one pixel value.
- `Relative` uses a fraction of the parent size.
- `Fill` shares the remaining parent space.
- `Fit` sizes to the children.
- `Shrink` sizes to the children and allows a clipped scroll viewport.

Set `LayoutDirection` to `Horizontal` or `Vertical` to place children in a row
or a column. `Spacing` adds space between children. `Padding` insets the child
area. A parent with no size constraint fits its children. A parent with `Fill`
or `Relative` resolves after its own parent.

`Reef.Tests/UIBuilderLayoutTests.cs:10` covers relative sizes, fit and shrink,
max bounds, fill distribution, cross-axis shrink, wrapped text, scroll content
size, and property assignment after child declarations.

Use `Fill` for flexible panels, rows, and columns. Use `Fit` for content-sized
groups. Use `Fixed` for stable control metrics such as a button height. Avoid
absolute `X` and `Y` values unless the element is an overlay, a radial control,
or another case where a fixed position is the intended geometry.

`Anchors.Center` is resolved along an axis only when the parent's layout is not
aligned along that axis. Use `LayoutDirection.None` for full-screen overlay
roots that center a fixed-size modal. `Reef.Tests/UIBuilderLayoutTests.cs:10`
covers this.

## Paint order

Elements paint in declaration order. The parent paints first, then the first
child, then later siblings. A later sibling paints on top of an earlier sibling.
Root elements follow the same rule.

Declare backgrounds first and overlays last. Do not rely on first-child-on-top
ordering. Prefer a parent background over a sibling background. A parent always
paints behind its children.

Interaction uses the reverse command order, so the topmost element receives
input first. The reverse interaction loop is at `Reef/UIBuilder.cs:399`.
Children are pushed in reverse at `Reef/UIBuilder.cs:994` so they paint in
declaration order.

An active drag keeps its held state after the cursor leaves the source. It does
not consume hover from other elements, so a drag target can highlight while the
source still receives `Held`.

## Style

`Reef/UI/Style.cs:5` holds:

- `Color` and `BackgroundColor`.
- `Padding`.
- `CornerRadius`.
- `Border`.
- `CornerCaps`.

`CornerRadius` is applied by `Reef/PixelRenderer.cs`. The OpenGL renderer does
not apply it.

### Border and corner caps

`BorderStyle` is at `Reef/UI/BorderStyle.cs:6`. It has a thickness and a color.
The border draws inside the element bounds. A zero thickness disables it.

`CornerCapStyle` is at `Reef/UI/CornerCapStyle.cs:6`. It has an arm length, an
arm thickness, and a color. Each corner gets an L shape. Caps draw over the
border. Zero values disable them.

The builder exposes both fields at `Reef/UIBuilder.cs:98` and `:104`.

Game pages do not use raw styles. They use the shared theme, metrics, and
widgets in [ui-theme](ui-theme.md).

The pixel renderer draws both at `Reef/PixelRenderer.cs:134` and `:151`. The
OpenGL renderer adds both to the command's rect vertex list at
`Swordfish/Graphics/SilkNET/OpenGL/Renderers/ReefRenderer.cs:178` and `:195`.
This keeps one draw call per element.

## Text

`UIBuilder.DefaultFontID` at `Reef/UIBuilder.cs:173` applies a font ID to text
that does not specify one. An explicit `FontOptions.ID` wins. The game sets this
once in `DefaultUIRenderer`.

`FontOptions` holds the size, no-wrap flag, horizontal alignment, and vertical
alignment. `FontOptions.NoWrap` is at `Reef/UI/FontOptions.cs:14`. The default is
false, so text wraps to the available width. When true, `Typeface` ignores the
maximum width and lays the text out on one line. The element or its parent must
clip the overflow.

`HorizontalAlignment` and `VerticalAlignment` place the text block inside an
element that is larger than the text. Use a `Fill` size and an alignment when a
label must center in a row or a panel.

The no-wrap checks are at `Reef/MSDF/Typeface.cs:88` and `:197`.

## Clipping and scrolling

`Viewport.ClipConstraints` creates an explicit clip rect. The rect is relative
to the element origin. It intersects the parent clip rect. `Viewport.Offset`
shifts child positions. Callers own the scroll value and the clamp.

Set `VerticalScroll` or `HorizontalScroll` to true to opt an element into the
scroll routing. `GetElementScrollDelta` returns the wheel delta that targeted the
element on the previous frame. Use it with the caller-owned scroll offset.
`GetElementBounds` returns the previous frame rect, clip rect, and content size,
so a scrollbar can size its thumb without guessing. The settings page and the
menu lists use this pair through `Widgets.ScrollArea`.

Interaction hit testing uses the intersection of the element rect and its clip
rect. A clipped control cannot receive hover or click outside the clip. The
check is in `Reef/UIController.cs:480`, and `UIBuilder` passes both rects at
`Reef/UIBuilder.cs:413`.

## Renderers

- `Reef/PixelRenderer.cs` renders to a CPU pixel buffer. Tests use it.
- `Swordfish/Graphics/SilkNET/OpenGL/Renderers/ReefRenderer.cs` renders through
  OpenGL. It reads each `RenderCommand` and builds rect and text vertices.

## Tests

- `Reef.Tests/UIBuilderBorderTests.cs:15` checks border and cap pixels.
- `Reef.Tests/UIBuilderBorderTests.cs:57` checks that zero styles change nothing.
- `Reef.Tests/TextLayoutTests.cs:12` checks `NoWrap`.
- `Reef.Tests/TextLayoutTests.cs:28` checks the default wrap behavior.
- `Reef.Tests/UIControllerClipTests.cs:8` checks clipping hit testing.
- `Reef.Tests/UIBuilderDefaultFontTests.cs:8` checks the default font fallback
  and explicit font override.
- `Reef.Tests/UIBuilderZOrderTests.cs:9` checks paint order and interaction
  order.
- `Reef.Tests/UIBuilderLayoutTests.cs:349` checks that explicit element queries
  do not change the current element ID.

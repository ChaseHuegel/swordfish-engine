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

The layout engine is not a CSS flexbox replacement. Dense screens should prefer
fixed-metric rectangles. `Fill` and `Relative` are appropriate for simple
containers, but they can shrink content below its natural height when a screen
overflows.

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

The builder exposes both fields at `Reef/UIBuilder.cs:86` and `:92`.

The pixel renderer draws both at `Reef/PixelRenderer.cs:130` and `:147`. The
OpenGL renderer adds both to the command's rect vertex list at
`Swordfish/Graphics/SilkNET/OpenGL/Renderers/ReefRenderer.cs:174` and `:191`.
This keeps one draw call per element.

## Text

`FontOptions.NoWrap` is at `Reef/UI/FontOptions.cs:9`. The default is false, so
text wraps to the available width. When true, `Typeface` ignores the maximum
width and lays the text out on one line. The element or its parent must clip the
overflow.

The no-wrap checks are at `Reef/MSDF/Typeface.cs:84` and `:167`.

## Clipping and scrolling

`Viewport.ClipConstraints` creates an explicit clip rect. The rect intersects
the parent clip rect. `Viewport.Offset` shifts child positions. Callers own the
scroll value and the clamp. The settings screen computes both from its fixed
layout.

Interaction hit testing uses the intersection of the element rect and its clip
rect. A clipped control cannot receive hover or click outside the clip. The
check is in `Reef/UIController.cs:373`, and `UIBuilder` passes both rects at
`Reef/UIBuilder.cs:301`.

## Renderers

- `Reef/PixelRenderer.cs` renders to a CPU pixel buffer. Tests use it.
- `Swordfish/Graphics/SilkNET/OpenGL/Renderers/ReefRenderer.cs` renders through
  OpenGL. It reads each `RenderCommand` and builds rect and text vertices.

## Tests

- `Reef.Tests/UIBuilderBorderTests.cs:15` checks border and cap pixels.
- `Reef.Tests/UIBuilderBorderTests.cs:57` checks that zero styles change nothing.
- `Reef.Tests/TextLayoutTests.cs:11` checks `NoWrap`.
- `Reef.Tests/TextLayoutTests.cs:27` checks the default wrap behavior.
- `Reef.Tests/UIControllerClipTests.cs:11` checks that a clipped control does
  not receive a click outside the clip.
- `Reef.Tests/UIControllerClipTests.cs:33` checks that a control inside the
  clip still receives a click.

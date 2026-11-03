# Proposal: Batched Reef UI rendering

- Status: options C, D, and E are implemented. In-game measurement is the only
  open item.
- Issue: [#690](https://github.com/ChaseHuegel/swordfish-engine/issues/690)
- Scope: the engine tier only. The renderer is in
  `Swordfish/Graphics/SilkNET/OpenGL/`, the batch type is in `Swordfish/UI/Reef/`.
  Reef (`Reef/`) keeps its API.

## 1. Summary

The OpenGL Reef renderer used to issue one draw call per element, and it
re-uploaded the whole vertex buffer once per draw. A dense page cost 67 draw
calls and 67 buffer uploads per frame, and each element paid a reflective
dictionary lookup that is 72 times slower than a plain integer key.

Paint order was the only reason to split the frame into draws. The renderer now
writes every primitive into one buffer in paint order, breaks the batch only
when the draw state changes, and samples one packed atlas. A dense page without
images is one draw call and one upload. The gallery below draws one image, so it
needs three.

`MeshBatch` is not the right tool for Reef. See section 7.

## 2. What the issue states

> Currently the OpenGL Reef renderer is pretty expensive since it creates a
> draw call per element.

Confirmed. See section 3.

## 3. Measurements

Taken with a throwaway test that builds the Reef Markup visual gallery
(`Reef.Markup.Tests/Fixtures/GalleryView.rml`, 68 markup tags) through the real
`UIBuilder`, then replays the renderer's state resolution. The test is not
committed.

| Metric | Value |
|---|---|
| Render commands | 56 |
| Commands with text | 15 |
| Visible glyph quads | 206 |
| Visible rect quads | 48 |
| Total quads, before border and corner-cap quads | 254 |
| Distinct clip rects | 2 |
| Image materials | 1 |
| Draw calls today | 67 |
| Draw calls, merged by draw state, before the atlas | 23 |
| Draw calls, merged by one shared state, no images | 1 |

The 67 is one rect draw per visible command plus one text draw per text command.
Every command also emitted a rect quad when the background alpha was zero, so
invisible elements cost a draw call too.

Dictionary cost:

| Key type | Cost per operation |
|---|---|
| `Dictionary<RenderCommand<PixelTexture>, int>` | 943 ns |
| `Dictionary<int, int>` | 13 ns |

The 72 times difference is real and expected. `RenderCommand<TRendererData>`
(`Reef/RenderCommand.cs:12`) has reference fields and does not implement
`IEquatable<T>`. `Dictionary` therefore calls `ValueType.Equals` and
`ValueType.GetHashCode`, which walk the fields through reflection.

The renderer used that key for the whole per-frame batch table
(`ReefRenderer.Instances`, before the change), so each command paid the
reflective path about two and a half times per frame: one failed lookup, one
hash, and one insert.

## 4. Cost before the change

Per draw call, in the previous `Draw` and `DrawArrays` methods:

1. The shared vertex buffer was updated for that element only. The buffer was
   created with `BufferUsageARB.StaticDraw` (`BufferObject.cs:11`), so the driver
   was told the data never changes, and `UpdateData` wrote with `BufferSubData`
   over a region that the previous draw was still reading
   (`BufferObject.cs:38-57`). That pattern either stalls the pipeline or forces
   the driver to rename the buffer. It happened once per draw call.
2. Depth test and polygon mode were set again, though both are constant for the
   whole pass.
3. One `MultiDrawArrays` call per element, with a single count.
4. In the untextured path, a `new int[4]`, a `glGetIntegerv(GL_VIEWPORT)`, and a
   `screenSize` uniform, per draw. No UI shader declared that uniform, so the
   uniform was dead and the viewport query was wasted.
5. A `BindMaterial` lookup, a shader bind, and a texture bind.

Per element, in the previous `PreRender`:

1. Two to three reflective dictionary operations on the `RenderCommand` key.
2. A fresh `Vertices` object, which is three `List<T>` allocations, per command
   and per frame. Nothing was pooled.
3. `Data.Capacity += 56` for every quad. Setting the capacity by a fixed step
   defeats the doubling policy of `List<T>`, so a long list copies its buffer
   quadratically.
4. `MathS.RangeToRange` twice per vertex for x and y.

Not measured: driver-side draw dispatch cost and GPU buffer synchronisation.
Those need a GPU, and Linux cannot run this renderer. The in-game
`PerformanceStatsOverlay` shows the draw-call count
(`WaywardBeyond.Client/UI/PerformanceStatsOverlay.cs:40`), so the draw-call
change is easy to verify in game. Frame time needs a before and after capture.

## 5. Why the previous renderer did not batch

A comment block in the previous renderer explained it. An earlier version
grouped draws by material and font. Images then drew after simple rects, and a
parent image painted over a child rect or text. Depth testing cannot fix that,
because a parent and its child can sit in different draw calls.

So the previous renderer used one draw per element to keep paint order. That is
correct but expensive. A batch can also keep paint order if it only merges
*consecutive* commands. See section 6, option C.


## 6. Options

| Option | Draw calls | Per-frame uploads | Per-frame allocations | Risk | Verdict |
|---|---|---|---|---|---|
| A. Today | `commands + text commands` | one per draw | 3 lists per command | none | Current problem |
| B. `MeshBatch` per element | unchanged | many small range writes | range copies | high | Rejected, section 7 |
| C. One buffer, one upload, multi-draw per state run | number of state runs | 1 | 0 after warm-up | low | **Implemented** |
| D. C plus one shared shader and texture | 1 | 1 | 0 | medium | **Implemented** |
| E. D plus instanced quads | 1 | 1 | 0 | medium | **Implemented**, cuts per-quad bytes 3.6x |
| F. Sort by material, ignore order | few | 1 | 0 | wrong pixels | Rejected |

Option C keeps exact paint order, so it cannot produce the regression described
in that comment. It merges only runs of commands that share the same
shader and texture, and those runs are already adjacent in paint order.

## 7. `MeshBatch` verdict

Do not use `MeshBatch` for Reef.

`MeshBatch` (`Swordfish/Graphics/MeshBatch.cs:8`) is a world-space batching
resource. It owns arena buffers with a size cap, stable `MeshRangeHandle`
values across compaction, per-range bounding boxes, a model matrix, frustum
culling, and version-based uploads
(`Swordfish/Graphics/SilkNET/OpenGL/Renderers/GLMeshBatchRenderer.cs:53-115`).
It exists so geometry that lives for many frames is written once and drawn many
times. The chunk-batching proposal
([voxel-chunk-batching](voxel-chunk-batching.md)) documents that design.

Reef rebuilds every primitive every frame, in screen space, in paint order.

| Requirement | `MeshBatch` | Reef |
|---|---|---|
| Lifetime | many frames | one frame |
| Update pattern | occasional range rewrite | full rebuild |
| Draw order | any order, depth tested | strict paint order, no depth |
| Vertex layout | 13 floats, mesh normals | 14 floats, baked clip rect |
| Transform | one model matrix | none, screen pixels |
| Culling | frustum per range | clip rect, already applied |

To use `MeshBatch`, Reef would allocate or update one range per element per
frame. Each of those calls takes `SyncRoot` (`MeshBatch.cs:77`), copies the
payload into a second array (`:108-109`), and can trigger compaction (`:159`).
`GLMeshBatchRenderer` would then still issue one multi-draw per range with no
promise about the order of ranges, which is the one property Reef must keep. The
model matrix, the depth path, and the frustum tests are all dead weight here.

The result is more CPU work per frame than today, with no reduction in draw
calls.

What is worth borrowing is the *shape* of the idea: one buffer, contiguous
ranges, multi-draw per pass. Option C applies that shape inside the
screen-space renderer, where it is a few hundred lines instead of a resource
with a lifetime.

`MeshBatch` stays the right tool for voxel chunks and other long-lived geometry.

## 8. Design, option C

Implemented. One pass, one vertex buffer, one upload, and one multi-draw per
state run.

### 8.1 Build

`ReefBatch` (`Swordfish/UI/Reef/ReefBatch.cs`) keeps a pooled vertex array and a
pooled run table, and resets the counters each frame.

- `Begin(width, height)` clears the frame and caches the pixel-to-clip scale.
- `UseState(state)` selects the draw state for the quads that follow.
- `AddQuad(rect, uv, color, clipRect)` appends one quad of four vertices.
- `End()` closes the trailing run.

Append rules, per command:

1. Append the background rect quad when its alpha is above zero. Invisible
   elements are skipped.
2. Append the border quads, then the corner-cap quads, as before.
3. Append one quad per visible glyph, with the same clip test as before.
4. Start a new run when the resolved draw state changes. The draw state is the
   typeface material for glyphs, the command material for images, and the
   default shader for plain rects.

A run holds only commands that already sit next to each other in paint order, so
the visual result is identical. A state selection is applied by the next quad,
so a state that draws nothing never splits a run.

The renderer resolves each `Material` to a `GLMaterial` through
`GLRenderContext.BindMaterial` and keeps a per-frame list. A run stores the
1-based index into that list. Runs compare by reference.

### 8.2 Draw

Superseded by section 10, which replaces the multi-draw with one instanced draw.
The version below is what option C shipped:

```csharp
vao.Bind();
_gl.Set(EnableCap.DepthTest, false);
_gl.PolygonMode(FrontAndBack, wireframe ? Line : Fill);
vao.VertexBufferObject.UpdateStreamingData(_batch.Vertices);

for (var i = 0; i < _batch.RunCount; i++)
{
    ReefBatch.Run run = _batch.GetRun(i);
    using GLMaterial.Scope _ = _runMaterials[run.State - 1].Use();
    _gl.MultiDrawArrays(TriangleFan, offsets, counts, (uint)run.QuadCount);
}
```

Every quad stays a four-vertex triangle fan. The per-quad offsets and counts are
passes as spans of the same pooled arrays, so a run is one draw call regardless
of how many quads it holds.

`BufferObject.UpdateStreamingData` (`BufferObject.cs`) replaces the whole buffer
storage with `StreamDraw` usage. That lets the driver hand out a fresh allocation
instead of waiting for draws that still read the previous frame.

The `screenSize` uniform and its `glGetIntegerv` call are gone. No UI shader
declared that uniform, so the call was dead and the query cost a driver state
read per draw.

### 8.3 Result

| Case | Before | Option C |
|---|---|---|
| Draw calls, dense page | 67 | 23 |
| Buffer uploads per frame | 67 | 1 |
| Reflective dictionary operations | ~140 | 0 |
| Per-frame allocations | 3 lists per command | 0 after warm-up |
| `glGetIntegerv` per frame | 52 | 0 |
| GL state calls per frame | ~200 | 3 |
| `List` capacity copying | quadratic | amortised |

The 23 runs on the gallery are the rect and text commands alternating. A page
that alternates plain rects and labels one for one gets about one run per
element. The worst case is no better than before. Section 9 removes that ceiling.


## 9. Design, option D

Implemented. Rects and glyphs sample one packed atlas, so a frame without images
is one draw call.

The first plan was a `Texture2DArray` with one slice per typeface. Two things
ruled it out:

- A texture array needs equal slice sizes. The typeface atlases range from
  152x152 to 1272x1272, so every slice has to be padded to the largest. That is
  17 slices of 1272x1272, about 110 MB.
- `TextureArray` already assumes equal slices and concatenates the pixel arrays
  blindly (`Swordfish/Graphics/TextureArray.cs:22-37`).

A packed 2D atlas costs about 20 MB for the same set, so the change packs into
one ordinary texture instead.

### 9.1 Atlas

`ReefAtlas` (`Swordfish/UI/Reef/ReefAtlas.cs`) packs RGBA images into one buffer
with a shelf packer. Sources are placed tallest first. Each source gets a
`Region` with its offset and size, plus the two UV mappings:

```csharp
float MapU(float u, int atlasWidth) => (OffsetX + u) / atlasWidth;
float MapV(float v, int atlasHeight) => (OffsetY + Height - v) / atlasHeight;
```

`MapV` applies the vertical flip that `ui_reef_msdf.glsl` used to apply in the
shader, so the sampled texel is the same one as before.

Measured on the shipped font set, which holds 17 atlases:

| Metric | Value |
|---|---|
| Source pixels | 16 MB |
| Packed atlas | 2083x2608, 20 MB |
| Load all atlases | 78 ms |
| Pack | 15 ms |

The pack is tight. There is no slice padding and no per-typeface texture.

### 9.2 One shader

`ui_reef_atlas.glsl` replaces `ui_reef.glsl` and `ui_reef_msdf.glsl`, which are
deleted. It samples a plain `sampler2D` at the UVs the CPU wrote.

Rects need no mode flag and no branch. A packed 4x4 block of opaque white
resolves to a signed distance of one, so the existing MSDF expression already
returns an alpha of one:

```
signedDistance = median(1, 1, 1) = 1
screenPxDistance = screenPxRange * (1 - 0.5) = 1 * 0.5
alpha = clamp(0.5 + 0.5, 0, 1) = 1
```

`screenPxRange` clamps to one for any atlas wider than 60 pixels, so the text
result is unchanged from the previous shader.

### 9.3 Images

Image materials keep `ui_reef_textured.glsl` and keep their own draw run. Only
the fragment body differs from the atlas shader; the vertex contract is shared,
see section 10.1.

Folding image materials into the atlas would replace a material's shader with the
atlas shader. A UI material can carry any shader, so that is a behaviour change
for game content, not an engine-only optimisation. A page that draws an image
needs one extra run per image instead of one for the frame. That is acceptable:
the shipped game has one image, on the title screen.


## 10. Design, option E

Implemented. A quad is now one instance of a four-vertex fan instead of four
vertices, so the color and the clip rect are stored once instead of four times.

### 10.1 Instance record

Sixteen floats, 64 bytes:

| Field | Floats | Attribute |
|---|---|---|
| clip-space bounds x0, y0, x1, y1 | 4 | location 8, `in_rect` |
| texture coordinates u0, v0, u1, v1 | 4 | location 9, `in_uvRect` |
| color | 4 | location 1, `in_color` |
| clip rect | 4 | location 3, `in_clipRect` |

Every instanced attribute uses a divisor of one, so the four vertices of an
instance read the same record. `ui_reef_atlas.glsl` and `ui_reef_textured.glsl`
both use this record and this corner expansion, so one vertex array serves every
UI primitive. The vertex stage derives the corner from `gl_VertexID` and expands
it:

```glsl
const vec2 CORNERS[4] = vec2[4](
    vec2(0.0, 0.0), vec2(1.0, 0.0), vec2(1.0, 1.0), vec2(0.0, 1.0)
);
vec2 corner = CORNERS[gl_VertexID];

ClipRect = in_clipRect;
VertexPosition = vec3(mix(in_rect.xy, in_rect.zw, corner), 0.0);
TextureCoord = vec3(mix(in_uvRect.xy, in_uvRect.zw, corner), 0.0);
```

The corner order is unchanged, so the triangle fan and its winding are the same
as before. Locations 0 and 2 are left disabled, which reads the defined default
of `(0, 0, 0, 1)`; the prologue assigns those to `VertexPosition` and
`TextureCoord` and `vertex()` overrides both. The instance texture coordinates
are named `in_uvRect` because the prologue already declares `in_uv`.

### 10.2 Draw

```csharp
_gl.DrawArraysInstancedBaseInstance(
    PrimitiveType.TriangleFan,
    first: 0,
    count: 4,
    instancecount: (uint)run.InstanceCount,
    baseinstance: (uint)run.BaseInstance
);
```

`baseinstance` is core 4.2, and the engine already requires 4.3 for its
`#version 430` shaders and compute passes. A run is contiguous in the instance
buffer, so base instance is all that a multi-run frame needs.

### 10.3 Cost

| Per quad | Option D | Option E |
|---|---|---|
| Vertex bytes | 224 | 0 |
| Instance bytes | 0 | 64 |
| Offset and count entries | 8 | 0 |
| **Total** | **232 B** | **64 B** |
| Primitive batches submitted | 1 | 1 |
| Draw calls | 1 | 1 |

On the gallery page, which emits 254 visible quads before border and cap quads:

| | Option D | Option E |
|---|---|---|
| Bytes uploaded per frame | ~59 KB | ~16 KB |
| Float stores per frame | ~17k | ~5k |
| Four-vertex fans submitted | ~254 | 1 |

The fan count is the part that was worth having. `MultiDrawArrays` still submits
one fan per quad, and on most desktop drivers it is expanded into a loop, so
option D left the per-primitive dispatch cost in place. Option E is one
instanced draw.

### 10.4 Risks taken

- **Base instance.** Core 4.2. Covered by the existing 4.3 requirement.
- **Divisor-one attributes.** Already used by `GLMeshBatch` and `GLRenderTarget`
  for the model matrix, so the engine depends on it regardless.
- **Pixel alignment.** The pixel-to-clip mapping stays on the CPU and produces
  the same floating point values as before, so only the corner expansion moved,
  and that is an exact `mix` of two stored bounds.
- **Shader validation.** The assembled vertex and fragment sources both compile
  and link under `glslang` 16.6, which also proved the harness catches a
  deliberate error.
- **Image shaders.** The first version of this change left `ui_reef_textured.glsl`
  reading `in_position` and `in_uv`, which the instanced array no longer enables,
  so every image drew as a degenerate triangle. An in-game smoke test caught it
  and `Swordfish.Tests/ReefShaderContractTests.cs` now holds both UI shaders to
  the instance contract.

### 10.5 What it does not change

The draw call count, the upload count, the atlas, the fragment shader, and the
layout pass in `UIBuilder.Build`. The layout pass is the largest remaining CPU
cost of a UI frame and is out of scope in section 12.


## 11. Fixes that landed with option C

Small, local, and independent of the batching design:

- Deleted the dead `MaterialRenderCommandComparer`.
- Deleted the stale TODO block and replaced it with one line on the run rule.
- Replaced `Data.Capacity += 56` with pooled growth.
- Deleted the dead `screenSize` uniform and its `glGetIntegerv` call.
- Skipped quads with zero alpha.
- Precomputed the pixel-to-clip scale once per frame instead of calling
  `MathS.RangeToRange` per coordinate.

`GLScreenSpaceRenderer` had the same one-upload-per-target shape, the same
fixed-step capacity growth, and set state per target. It is fixed the same way
in its own change, on the `BufferObject` streaming path. No game content creates
a `RectRenderer`, so that change is consistency and safety, not a measured win.

## 12. Out of scope

- The layout pass in `UIBuilder.Build`. It is likely the larger CPU cost of a UI
  frame, and it is a different subject.
- `PixelRenderer`, which is CPU-side and only used by tests.
- Corner radius, which the OpenGL renderer does not apply today
  ([reef-ui](../specs/reef-ui.md)).
- Order-independent transparency, depth-based UI sorting, and GPU-driven
  indirect draws.

## 13. Risks

| Risk | Mitigation |
|---|---|
| A run breaks in the wrong place and changes paint order | Merge only adjacent commands with the same state. Add a renderer test that compares quad order against the command order. |
| One large upload per frame stalls the driver | Orphan the buffer first. Profile in game with the F3 overlay. |
| The pooled buffer never shrinks after a large frame | Cap the retained size and shrink after a few idle frames. |
| The one-upload path changes other renderers | Add the streaming path as a new `BufferObject` method. Do not change `UpdateData`. |
| The mode branch slows down text fragments | Only relevant to option D. Measure before keeping. |
| Headless Linux cannot verify pixels | Rely on the existing golden-image tests for layout, and require one in-game before and after capture for the draw-call count. |

## 14. Verification

The batch build and the atlas are GL-free types, so they are tested directly:

- `Swordfish.Tests/ReefBatchTests.cs` covers run grouping, paint order, dropped
  empty runs, invisible quads, the clip-space mapping, and growth.
- `Swordfish.Tests/ReefAtlasTests.cs` covers region bounds, overlap, pixel
  placement, packing density, and the UV mapping.
- `Swordfish.Tests/TypefaceAtlasLoadTests.cs` covers loading a shipped atlas and
  matching it to its atlas description.

Outstanding: one in-game capture of `Draw calls` from the F3 overlay for the main
menu, the settings page, and one gameplay frame, plus frame time for the same
three frames with the overlay hidden. This machine cannot run the OpenGL
renderer.

## 15. Result

A dense page went from 67 draw calls to one, with one buffer upload per frame,
no reflective dictionary work, and no per-frame allocation after warm-up. A page
that draws an image material needs one more run per image, so the gallery goes
from 67 to three. Paint order is preserved by construction, so no element can
reorder against another.

Per quad, the frame buffer dropped from 232 bytes to 64, and the roughly 254
four-vertex fans became one instanced draw. Draw call count and upload count are
unchanged by option E; the fan count is what it removes.

The remaining cost is the layout pass in `UIBuilder.Build`, which section 12
leaves out of scope, and the per-quad vertex volume that option E would reduce.

The trade-off to watch is GPU memory. The packed atlas uploads every typeface up
front, about 20 MB, where the previous renderer uploaded only the typefaces it
drew. Packing only the used typefaces is possible, and would need an atlas
rebuild when a new one appears. Do that only if 20 MB shows up in a memory
budget.

## 16. References

- `Swordfish/Graphics/SilkNET/OpenGL/Renderers/ReefRenderer.cs`
- `Swordfish/Graphics/SilkNET/OpenGL/Renderers/GLScreenSpaceRenderer.cs`
- `Swordfish/Graphics/SilkNET/OpenGL/BufferObject.cs`
- `Swordfish/Graphics/MeshBatch.cs`
- `Swordfish/Graphics/SilkNET/OpenGL/Renderers/GLMeshBatchRenderer.cs`
- [voxel-chunk-batching](voxel-chunk-batching.md), for the batch design that
  `MeshBatch` implements.
- [reef-ui](../specs/reef-ui.md)

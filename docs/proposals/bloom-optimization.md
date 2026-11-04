# Bloom Optimization — Proposal and Development Plan

Status: implemented. The float bloom source remains as follow-up.

Scope: the engine rendering tier (`Swordfish`), the forward+ OpenGL pipeline, the
shader library, and the bloom settings exposed to `WaywardBeyond.Client`.

## 1. Summary

Bloom is the most expensive post-process in the forward+ pipeline. On integrated
GPUs it costs more than the scene lighting pass in some frames. The cost comes
from four defects, not from the idea of bloom:

1. The blur runs at full swapchain resolution, in place, for 11 passes.
2. The blur reads and writes one texture. The graphics API does not define that
   operation.
3. Two full-resolution multisample resolves feed one bloom frame. The second one
   exists only to test a per-pixel discard.
4. Every multisample blit in the bloom path passes a linear filter. The API
   requires a nearest filter for a multisample read buffer.

The proposal replaces the path with a half-resolution, two-texture ping-pong
blur. It keeps the emissive-driven look and keeps the emissive color attachment,
because only that attachment can distinguish an emissive surface from a merely
bright one. It removes one full-resolution render target and one full-resolution
resolve. It makes the pass count a real quality control instead of a linear cost
multiplier.

## 2. Current state

### 2.1 Frame path

`ForwardPlusRenderingPipeline<TRenderStage>` creates a multisampled render
target with two color attachments
(`Swordfish/Graphics/SilkNET/OpenGL/Pipelines/ForwardPlusRenderingPipeline.cs:231-234`):

| Attachment | Format | Samples | Size |
|---|---|---|---|
| `ColorAttachment0` | `Rgba16f` | 4 | full |
| `ColorAttachment1` | `Rgba8` | 4 | full |
| depth/stencil | `Depth24Stencil8` | 4 | full |

Scene shaders write the emissive value to `ColorAttachment1` as `BrightColor`.
The parser declares that output for every shader
(`Swordfish/IO/GlslParser.cs:109`) and defaults it to black
(`Swordfish/IO/GlslParser.cs:130`). The client writes emissive texels into it
(`WaywardBeyond.Client/assets/shaders/lightedArray.glsl:46-53`).

`PostRender` then runs the bloom path
(`ForwardPlusRenderingPipeline.cs:504-568`):

1. Resolve `ColorAttachment1` into `_blurTex` (full resolution, `Rgb16f`) — line 508.
2. Run `Passes + 1` separable Gaussian passes over `_blurTex`, in place — lines 517-535.
3. Resolve `ColorAttachment1` a second time into `_screenTex` (full resolution,
   `Rgba16f`) — line 540. This copy exists only to test `discard` in the
   composite shader (`Swordfish/assets/shaders/bloom.glsl:17-21`).
4. Composite the blurred `_blurTex` over the back buffer with additive blending —
   lines 548-565.

`Bloom.Enabled` defaults to `true` and `Bloom.Passes` defaults to `10`
(`Swordfish/Settings/BloomSettings.cs:7-8`,
`WaywardBeyond.Client/UI/Settings/SettingsDefaults.cs:19`). The loop at line 524
is `i <= Passes`, so the default frame runs 11 full-resolution blur draws.

### 2.2 Cost model

At 1920×1080 the frame shades 2.07 M pixels. Each Gaussian pass fetches 9 texels.
The default frame therefore issues:

```
11 passes × 9 fetches × 2.07 Mpx = 205 M texel fetches per frame
```

At 60 fps that is 12.3 G fetches/s. Each fetch of an unfiltered-cached `Rgb16f`
texel reads about 6 bytes, so the blur alone moves on the order of 1 GB per
frame, or roughly 60 GB/s. A laptop with dual-channel LPDDR4X has about 40 GB/s
of usable bandwidth. Bloom alone exceeds it.

The two full-resolution multisample resolves add to the same budget. Each one
reads 4 samples × 2.07 M × 4 bytes and writes 8 MB.

### 2.3 Defects found

**B1 — In-place blur (undefined behavior).** The pass at
`ForwardPlusRenderingPipeline.cs:517-535` binds `_blurFBO` for draw and binds
`_blurTex` to `Texture0` for sampling in the same draw. The API leaves the
result undefined. Drivers either sample stale or partially written data, which
makes the passes produce a weak, smeared result instead of a wide blur. This is
why the current look depends on the pass count so heavily. The SAO blur at
lines 380-399 has the same defect at half resolution.

The source comment at lines 512-516 records the tradeoff as intentional: one
texture and one pass are "a bit more performant". Section 2.4 measures that
tradeoff.

**B2 — Multisample blit with a linear filter.** `FramebufferObject.Blit` passes
the filter through to `BlitFramebuffer`
(`Swordfish/Graphics/SilkNET/OpenGL/FramebufferObject.cs:100-105`). The engine
passed `BlitFramebufferFilter.Linear` on lines 501, 508, and 540, where the read
buffer is the 4× multisampled render target. The specification requires
`NEAREST` when the read buffer has samples. The same rules also forbid a
multisampled blit that changes the rectangle size, so a resolve cannot also
downsample. Both rules shape the design in 3.1.

**B3 — Wasted full-resolution copy.** `_screenTex` (line 215) is `Rgba16f` at
full resolution. The composite shader samples it only to compare
`length(emissive.rgb) != 0` (line 18 of `bloom.glsl`). That costs 16 MB of video
memory, one full-resolution resolve per frame, one full-resolution texture fetch
per pixel, and a divergent branch. The comparison is also an exact float test,
which is fragile.

The pixel mask itself is needed. The copy is not. Section 3.1 keeps the mask
semantics at no extra cost.

**B4 — Gaussian tap offset bug.** In
`Swordfish/assets/shaders/blur_gaussian.glsl:23-25` and `32-34` the offset is
scaled by the loop index twice (`texelSize * i` and then `offset * i`). The taps
land at 1, 4, 9, and 16 texels while the weights assume 1, 2, 3, and 4 texels.
The kernel is much wider than its weights, so it darkens the result.

**B5 — Pass count is a linear cost multiplier.** Each extra pass is one more
full-screen draw with no change in resolution. The setting is exposed to players
as a quality control but only adds cost.

**B6 — Sample count ignores the anti-aliasing setting.** The three render
targets are always allocated with 4 samples
(`ForwardPlusRenderingPipeline.cs:231-233`). `EnableCap.Multisample` is toggled
at line 310, which only disables rasterization coverage. The storage stays
multisampled, so a player who selects no anti-aliasing still pays for the
resolve of every attachment.

**B7 — The threshold uniforms cannot reach the emitters.** `uBloomThreshold` and
`uBloomSoftness` are declared in
`WaywardBeyond.Client/assets/shaders/lightedArray.glsl:11-12` and read by no
shader. The defaults cannot work as written. The emissive value comes from an
8-bit texture array (`WaywardBeyond.Client/Graphics/PBRTextureArraysParser.cs:106`),
so it ranges 0 to 1, and the bloom attachment is `Rgba8`, so it clamps at 1. A
threshold of `1.5` never selects a texel.

### 2.4 What the in-place blur actually buys

The in-place chain cost one texture allocation and one framebuffer bind per
pass. It did not change the work of a pass. A ping-pong pass issues the same
full-screen quad, the same 9 fetches, and the same single write. The only
difference is the destination handle.

So the tradeoff was 12 MB of video memory against defined behavior. The rest of
the redesign changes that arithmetic in both directions:

- At half resolution one chain texture is 4 MB, not 12 MB.
- The redesign deletes `_screenTex` (16 MB) and the full-resolution
  `_blurTex` (12 MB). A correct chain of two half-resolution textures is 8 MB.

The correct version therefore uses 20 MB less memory than the incorrect
version, and each pass is 4× cheaper. The tradeoff no longer exists on either
axis.

The in-place chain also made each pass unreliable. A pass could read stale or
partly written data, so how much blur a pass added depended on the driver and on
tile order. The other two defects pushed the same way: the tap offsets spread
each tap wider than its weight assumes (B4), so a pass lost energy and looked
dim, and there was no threshold (B7), so the only way to make bloom visible was
more passes. The default of 10 is a symptom control for those defects, not a
quality knob.

## 3. Design as implemented

### 3.1 Half-resolution mip chain

Bloom reads a chain of blur levels. Level 0 is half resolution, and each level
after it halves again. Every level blurs with two separable Gaussian passes
through a `DoubleBufferTarget`, moves down into the next coarser level, and the
coarser levels accumulate back into the finest one.

The multisample resolve stays at 1:1. A blit may not reduce the size of a
multisampled read buffer, so the downsample happens in the extract pass.

Frame path:

```
scene pass ──> ColorAttachment0 (HDR, MSAA)
           └─> ColorAttachment1 (emissive, MSAA)
                     │
                     │  NEAREST blit, 1:1  (one MSAA resolve)
                     ▼
                 _bloomResolveTex (full res, Rgba8)
                     │
                     │  extract: threshold, mask into alpha, 2x2 downsample
                     ▼
                 level 0 (half res, Rgba16f) ── 2 Gaussian passes
                     │  downsample
                     ▼
                 level 1 (quarter res) ── 2 Gaussian passes
                     │  downsample
                     ▼
                 level 2 (eighth res) ── 2 Gaussian passes
                     │
                     │  upsample x0.5, additive, back up the chain
                     ▼
                 level 0  (core + every radius summed)
                     │
                     │  composite: rgb * mask * intensity, additive
                     ▼
                 back buffer
```

Details:

- **Resolve.** One 1:1 `NEAREST` blit from `ColorAttachment1` into
  `_bloomResolveTex`. The extract pass samples it with bilinear filtering, which
  averages 2×2 texels and produces the level 0 source.
- **Levels.** `Bloom.Passes` counts blur levels, clamped to 1-4. Each level runs
  `BLOOM_PASSES_PER_LEVEL` passes. `MAX_BLOOM_LEVELS` chains are allocated once
  and resized with the window.
- **Accumulation.** Upsampling weights each level by `BLOOM_UPSAMPLE_WEIGHT`
  (0.5) and blends additively. A single radius looks soft. The sum of three
  radii gives a bright core plus a wide falloff, which is what reads as glow.
- **Strength.** `Bloom.Intensity` multiplies the composite, clamped to 0-8. A
  normalized blur cannot be both wide and bright, so a separate multiplier is
  required. See 3.2.
- **Threshold.** `Bloom.Threshold` and `Bloom.Softness` run in
  `bloom_extract.glsl`, where they belong (B7). The source attachment is still
  `Rgba8`, so the threshold range is 0-1. See 4.
- **Emissive mask.** `bloom_extract.glsl` writes `1 - step(1/255, max channel)`
  into alpha. The blur widens it into a soft falloff. The composite multiplies
  by it. No extra texture, pass, or resolve. `_screenTex` is gone.
- **Shared API.** `DoubleBufferTarget`
  (`Swordfish/Graphics/SilkNET/OpenGL/DoubleBufferTarget.cs`) owns two textures,
  their framebuffers, `Swap()`, and `Resize()`. The pipeline's `Blur` drives it
  from any blur program. The SAO blur can adopt both without a pipeline change.
- **Quality tiers.** `BloomQuality` in the client maps `Low`, `Medium`, and
  `High` onto level count and intensity, following the `ShadowQuality` preset
  pattern. The engine keeps the primitive values.

### 3.2 Strength is not radius

A normalized Gaussian conserves energy. Spreading one texel over a wider area
lowers the peak, so more levels make the halo wider and dimmer, not brighter.
That is why the old pass count could not compensate, and why a correct chain
looked weaker than the broken one it replaced.

No blur setting can fix that. Every renderer multiplies the result by an
intensity, which is why `Bloom.Intensity` exists and why its default is above 1.

### 3.3 Cost

At 1920×1080, measured in full-resolution pixel equivalents (one full-screen
pass = 1.0):

| Chain | Passes | Cost |
|---|---|---|
| Before, default | 11 at full res | 11.0 |
| Before, new defaults | 3 at half res | 0.75 |
| Now, 2 levels | extract + 2 blur pairs + 1 up | 1.0 |
| Now, 3 levels | extract + 3 blur pairs + 2 up | 1.3 |

Fetch count for the blur falls from 205 M to about 15 M. One full-resolution
resolve disappears. Video memory for bloom falls from about 62 MB to about
50 MB, and `AntiAliasing = None` now removes another 96 MB of render target
storage.

### 3.4 Phase plan

**Phase 1 — correctness and cheap wins.** Implemented.

1. **Separate commit, lands alone:** switch the three multisample blits to
   `NEAREST` (B2). b5ae7965.
2. Correct the Gaussian tap offsets, and blur alpha so the chain can carry the
   mask (B4). 66c25c3a.
3. Add `DoubleBufferTarget` and make the blur ping-pong at half resolution (B1).
   d8ea63e0.
4. Remove `_screenTex` and `_screenFBO`. Move the emissive mask into the chain
   alpha through `bloom_extract.glsl` (B3). d8ea63e0.
5. Clamp `Passes` and default it (B5). d8ea63e0, 9b9a0d6a.

The SAO blur keeps its in-place loop. Phase 1 stops at the shared API, so the
SAO work lands on its own branch.

**Phase 2 — tuning, radius, and allocation.** Implemented.

1. `Bloom.Intensity`, `Bloom.Threshold`, and `Bloom.Softness` replace the dead
   uniforms (B7). 22d8cdfc.
2. Mip chain and level count (B5). 51826c8f.
3. Release every pipeline owned handle, which recreation requires. fb341025.
4. Allocate render targets from the anti-aliasing setting (B6). f8275afd.
5. Client quality preset over level count and intensity. 0689ee39, 5b1c942c.

### 3.5 The emissive attachment is required

A bright-pass over `ColorAttachment0` cannot replace `ColorAttachment1`. A
luminance threshold selects every bright pixel: specular highlights, the sky,
sunlit snow, white brick. It cannot select emissive texels. Colored emissive
surfaces therefore need a separate signal from the scene pass, and
`ColorAttachment1` is that signal. Keep it.

That attachment has one real cost left: one extra color write per fragment in
the scene pass, at 4× multisample. This proposal does not remove it. It removes
everything around it.

One change is worth making now, while the code is moving. `Rgba8` clamps the
emissive value at 1.0, so a threshold above 1.0 never selects a texel and an
emissive intensity multiplier would clip. Store the attachment as
`R11G11B10F` (4 bytes, same as `Rgba8`, color renderable, filterable). Add the
format to `Swordfish/Graphics/SilkNET/OpenGL/TextureFormat.cs`, then apply the
threshold in the extraction pass. Verify that the framebuffer stays complete on
the target hardware, because the constructor throws on an incomplete one.

If the extra write becomes the binding cost on integrated GPUs, there is an
approximate alternative. Write a scalar emissive weight into the unused alpha of
`ColorAttachment0` and reconstruct the bloom color from the HDR color, which
already equals `max(lit, emissive)`. That removes the attachment entirely. It is
wrong where the lit color exceeds the emissive color on the same surface, so
treat it as a fallback that needs art approval, not the default.

## 4. Risks and open questions

- The soft alpha mask replaces a hard `discard`. Compare the edge of an emissive
  surface against the current output before tuning the threshold.
- The bloom source is still `Rgba8`, so `Bloom.Threshold` cannot exceed 1.0 and
  emissive intensity cannot exceed 1.0. Moving the attachment to
  `R11G11B10F` lifts both limits. No consumer needs that yet, so the change is
  deferred.
- `Bloom.Intensity` above roughly 4 clips the additive composite to white and
  shifts hue as channels saturate. The tonemapping gap is tracked in
  [issue 0098](../issues/0098-color-transfer-and-tonemapping.md).
- The half-resolution chain softens the smallest emissive texels. Compare
  against the current output at the same settings, and tune the default pass
  count with the art direction.
- Switching the multisample blits to `NEAREST` changes the current image on
  drivers that tolerate the invalid filter. Treat the old image as undefined.
  Land it as its own commit so the delta can be compared directly.
- `Rgb16f` is replaced by `Rgba16f`. Confirm on the target hardware that the FBO
  remains complete and that the byte cost does not rise.
- `R11G11B10F` has no alpha and no guaranteed blend support. Confirm the FBO is
  complete on the target hardware before keeping the change.

## 5. Validation

1. Capture a GPU time baseline on an integrated GPU with a RenderDoc capture or
   a `GL_TIME_ELAPSED` query around `PostRender`. Record a scene with heavy
   emissive content at 1080p.
2. Compare frame time with bloom disabled. The current difference is the budget
   that the change must reduce.
3. Confirm no OpenGL errors from `_glDebug.TryLogError()` in the bloom path at
   window resize, at each anti-aliasing mode, and at each level count.
4. Confirm the bloom target sharpness and radius against reference screenshots,
   and choose the default `Bloom.Intensity` and quality presets from that.
5. Confirm the render targets rebuild when the anti-aliasing setting changes.
6. Run `dotnet build` and `dotnet test Swordfish.Tests`.

## 6. Docs pass

- `docs/specs/config-schemas.md` records every bloom key and its range.
- `docs/specs/settings-ui.md:145` lists the bloom controls, now quality instead
  of pass count.
- `docs/proposals/dynamic-shadows.md:40` describes the bloom blur in its frame
  flow. The description stays true.
- `Bloom.Passes` is a level count, not a pass count. One level is two passes.

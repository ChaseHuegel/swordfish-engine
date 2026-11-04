# SAO Rendering Optimization — Proposal

Status: implemented. The GPU timing check in Phase 4 remains open.

Scope: the engine forward+ pipeline (`Swordfish`), the SAO shader, the render
settings, and the ambient occlusion settings in `WaywardBeyond.Client`.

Issue: [#689](https://github.com/ChaseHuegel/swordfish-engine/issues/689).

Rebased on `wayward-beyond` at `1412abec`.

## 1. Summary

Scalable Ambient Obscurance (SAO) is a large GPU cost on integrated GPUs. The
current pass does about 104 million texel fetches per frame at 1920×1080. Most
of that work is wasted.

This proposal cuts the cost by about 5x without a visible quality loss. The
main changes are:

1. Run SAO at half resolution. The code already asks for half resolution, but a
   bug cancels it.
2. Blur through the shared double-buffer target. The current blur reads and
   writes one texture in the same draw.
3. Remove a matrix multiply per SAO sample.
4. Clip SAO to a maximum distance.

The bloom work on `wayward-beyond` supplies the ping-pong target, the blur
driver, and the corrected Gaussian kernel. Phase 1 is now mostly plumbing. It
needs no new shader and no new render target class.

## 2. What the bloom work changed

Commits `7b22b02d` (Optimize bloom rendering) and `1412abec` (client bloom
settings) landed before this branch. They affect the plan in four ways.

| Change | Effect on this plan |
|---|---|
| `DoubleBufferTarget` (`Swordfish/Graphics/SilkNET/OpenGL/DoubleBufferTarget.cs`) owns two textures, two framebuffers, `Swap`, and `Resize` | The SAO blur adopts it. No new class. |
| `Blur(ShaderProgram, DoubleBufferTarget, int passes)` at `ForwardPlusRenderingPipeline.cs:753-772` drives any blur program through a ping-pong chain | The SAO blur calls it. No new blur loop. |
| The Gaussian tap offsets are correct in `blur_gaussian.glsl:21-26` | Phase 1 has no shader edit. |
| `GetRenderSamples` and `CreateRenderTargets` allocate render targets from the anti-aliasing setting (`ForwardPlusRenderingPipeline.cs:727-741`) | The old 4× MSAA finding is closed. |

Two earlier findings are therefore withdrawn:

- The squared tap offsets. Fixed upstream.
- The unconditional 4× multisample allocation. Fixed upstream.

The bloom proposal already records the SAO in-place blur as defect B1
(`docs/proposals/bloom-optimization.md:84-90`). That defect is still present.

## 3. Current cost

At 1920×1080 the frame contains 2.07 million pixels.

| Stage | Texel fetches per pixel | Total per frame |
|---|---|---|
| SAO pass (8 samples, 2 normal taps, 1 center tap) | 11 | 22.8 M |
| Blur (4 passes × 9 taps) | 36 | 74.6 M |
| `ao()` in `pbr_plus.glsl` (2 depth taps, 1 AO tap) | 3 | 6.2 M |
| **Total** | | **≈ 104 M** |

At 60 frames per second this is about 6.3 G texel fetches per second. Each SAO
sample also runs a full 4×4 matrix multiply. That is 16.6 M matrix multiplies
per frame. Integrated GPUs share memory bandwidth with the CPU, so the gather
pattern of a full-resolution SAO pass is close to the worst case.

The blur now uses correct tap offsets. The fetch count is unchanged, because
the fixed kernel still uses 9 taps per pass.

Costs are estimates from the shader source. Measure before and after on the
target laptop.

## 4. Findings

The line numbers in this section refer to the file before the change. Most of
the code they point at is gone.

### 4.1 The half-resolution path is dead

`ForwardPlusRenderingPipeline.cs:382` sets a half-resolution viewport. Line 385
binds `_ssaoFBO`. `FramebufferObject.BindHandle`
(`FramebufferObject.cs:79-83`) sets the viewport to the full framebuffer size.
The half-resolution viewport is overwritten before the draw. SAO runs at full
resolution.

The buffer is also allocated at full resolution
(`ForwardPlusRenderingPipeline.cs:221-222, 311`). The viewport at line 429 is
equally dead.

`_screenHalfWidth` is no longer a write-only field, because the bloom chain uses
it. Only the two SAO viewport calls are dead.

### 4.2 The blur reads and writes the same texture

`ForwardPlusRenderingPipeline.cs:406-427`:

```csharp
using (_blurShader.Use())
using (_ssaoFBO.Use())                                  // draw target
using (_ssaoTex.Activate(TextureUnit.Texture0))         // read source
```

`_ssaoTex` is attachment 0 of `_ssaoFBO`. Sampling the current draw target is
undefined behavior. Bloom now has the correct pattern at lines 671-689. SAO
still has the old one.

### 4.3 Each SAO sample rebuilds a view position with a matrix multiply

`ssao.glsl:26-31` multiplies a 4×4 matrix for every sample. The depth-to-view
conversion is a scale and a bias. Two uniforms cover it.

### 4.4 Normal reconstruction costs two more full-resolution taps

`ssao.glsl:88-93` reads two neighbor depths to build a normal. It is correct
on a plane, but a far-plane neighbor maps to a far position and poisons the
normal. The cost is 2.07 M extra fetches per frame at full resolution. The
final form reads four and picks the same-surface side.

### 4.5 The AO texture is `R32f`

`_ssaoTex` holds a value in 0..1. `R32f` costs 4 bytes per pixel and 8.3 MB at
full resolution.

### 4.6 The depth rejection test is a no-op

`pbr_plus.glsl:286-289` compares `texture(uDepth)` with `texture(uPreDepth)`.
`ShaderActivationCallback` binds the same `_depthTex` to both samplers
(`ForwardPlusRenderingPipeline.cs:963-967`). `step(aoDepth, fragDepth)` is
always 1. The test costs a fetch and rejects nothing. `gl_FragCoord.z` gives
the current fragment depth for free, but it cannot replace the test. See 4.8.

### 4.7 SAO covers the full scene

A radius of 0.1 view-space units at 60 m produces a sub-pixel offset. The pass
still spends full cost on distant geometry where the result is invisible.

### 4.8 Post-implementation artifact: dither from the depth rejection

The first implementation replaced the no-op test with `gl_FragCoord.z`. That
produced a per-pixel dither of the AO and thin horizontal light bands. The
prepass stores depth in a 24-bit texture. `gl_FragCoord.z` is full precision.
At equal depth the two differ by up to half a texel step, so `step()` flips on
roughly half the pixels. Bands appear where the rounding is consistent. The
forward pass and the prepass rasterize the same surface, so the test has no
value for opaque geometry. The fix removes the test and both depth samplers.
See [rendering-sao](../specs/rendering-sao.md).

### 4.9 Post-implementation artifact: streaks from a mis-weighted blur

Two defects in an earlier bilateral blur shader produced the streaks.

**The center tap used weight `1.0` instead of `WEIGHTS[0]`.** The normalization
kept the result in range, but the kernel was no longer a Gaussian. Its response
at the 4-texel noise period was 0.445 per pass, so 19.8 % of the tile survived
both passes. The intended Gaussian leaves 0.026 %. The tile is 4 AO texels, or
8 screen pixels at the default scale, which reads as a fine screen-aligned
pattern.

**The depth weight compared each tap against the center depth.** On a surface
that recedes along one screen axis, the taps along that axis differ from the
center by more than the threshold even though they lie on the same plane. The
weight faded them out, so the blur lost that axis and the tile survived as
vertical streaks.

A simulation of the two passes at 5 m with a 4-texel tile measures the x
residual:

| Case | x residual |
|---|---|
| Shipped | 0.0065 - 0.0082 |
| Center weight fixed | 0.00014 flat, 0.00058 at a 0.05 texel slope |
| Both fixed | 0.00014 - 0.00021, with x matching y |

The fix uses `WEIGHTS[0]` for the center and estimates the local slope from the
two neighbor depths along the blur axis. Each tap is compared against the
predicted plane. Steps still fade. Grazing planes keep both axes.

The slope form was still not enough. Two further forms were tried:

- **Slope prediction.** Each tap was compared against a plane fitted from the
  neighbor depths. The clamp saturated on steeply grazing surfaces, so the far
  taps were rejected and the blur lost an axis again.
- **Normal distance.** Each tap was projected onto the local normal. This fixed
  grazing surfaces, but it preserved every crease, which is the next finding.

The raw debug view settled it. The lines are already in the raw AO: a sharp dip
to 0.81 at every voxel crease and 1.0 on the faces, spaced by one voxel. They
are real contact shading, not the rotation tile. Any edge-preserving weight
keeps a crease sharp, so every bilateral form preserved the grid. The original
full-resolution pass used a wide, edge-agnostic blur that smeared the creases
into ambient shading. The final blur is the same idea: a plain 9-tap separable
Gaussian via `blur_gaussian.glsl`, with no depth weight.

The rotation has to be per pixel, because a fixed pattern leaves the sample
distribution itself visible at reduced resolution. A 4x4 tile has a 16 px
period and interleaved gradient noise has a 15 px period from its `0.067` term.
The final rotation is a white integer hash of `gl_FragCoord`, which has no
spatial period.

The blur stride interacts with the rotation. A stride of 3 taps only every
third AO texel, so adjacent output pixels read disjoint sets of the white noise
and the residual is a period-3 grid, about 7 px at 0.5. The paper's stride-3
form requires its 2x2 reconstruction pass first. This pipeline has no such
pass, so the stride is 1.

The remaining grid was the depth input. The AO sampled the full-resolution
depth on a half-resolution grid, and `depth_downsample.glsl` built that grid
with a single point sample. At 0.5 the AO centres fall at a half-texel phase
between source texels, so an angled plane became a staircase and the AO
reconstructed the staircase as bands. The downsample now averages the quadrant
centres of the source block that are below the far plane. Window depth is linear
across a plane, so the mean is the depth at the block centre. A sky tap is
ignored, so a silhouette block stays on the wall. That removed the bands.

The shipped configuration is half resolution by default: `0.25/0.5/0.5/1.0` for
`Low`/`Medium`/`High`/`Ultra`, with the 9-tap stride-1 blur and the distance clip.

## 5. Solution

### Phase 1 — Adopt the shared double-buffer path

Implemented.

1. Add `SAO.ResolutionScale` (default `0.5`) and `SAO.BlurPasses` (default `2`).
2. Replace `_ssaoTex` and `_ssaoFBO` with one `DoubleBufferTarget _ssaoChain`.
   Size it at the scaled resolution with `R16f` and linear filtering. Add the
   format to `TextureFormat.cs`.
3. Draw the SAO pass into `_ssaoChain.FrontTarget`. Call
   `Blur(_blurShader, _ssaoChain, SAO.BlurPasses)`. Use `DrawScreenQuad()`.
4. Bind `_ssaoChain.Front` at unit 5 in `ShaderActivationCallback`.
5. Resize the chain in `OnWindowResized`. Dispose it in `Dispose` in place of
   `_ssaoTex` and `_ssaoFBO`.
6. Delete the dead viewport calls in the SAO block.

The SAO pass writes the raw result into the chain front. `Blur` reads front and
writes back, then swaps. After each pass the destination becomes front, so the
result is in `Front` for any pass count. The helper already handles the swap,
the uniform, and the shader bind.

Result at 1920×1080 with 8 samples and the shipped configuration:

| Stage | Texel fetches per frame |
|---|---|
| SAO pass at 0.5 with manual bilinear depth taps | 19.2 M |
| Depth downsample, box average | 2.1 M |
| Blur, 2 passes × 9 taps at 0.5 | 9.3 M |
| `ao()` in the forward pass | 2.1 M |
| **Total** | **≈ 33 M** |

This is about 3.2x less fetch traffic than the original 104 M. The presets use
0.25/0.5/0.5/1.0 for `Low`/`Medium`/`High`/`Ultra`. The distance clip and the
removed depth test are additional wins.

The blur is a plain wide Gaussian. A bilateral depth weight was tried and
removed: at this voxel scale it preserved the crease at every block edge, which
reads as a grid. The final banding fix was upstream of the blur: the
AO-resolution depth is now a box average of the source block, not a point
sample. Point sampling turned angled planes into a staircase and the AO
reconstructed that staircase as bands.

### Phase 2 — Shader math

Implemented.

1. Replace the per-sample `UVToView` matrix multiply. Compute the view depth
   from the depth value with a scale and a bias. Compute `viewPos.xy` from the
   UV with a second scale. Pass both from the CPU.
   The engine uses a right-handed projection, so view Z stays negative. Keep
   the sign explicit.
   Pin the result with a unit test against `Matrix4x4.Invert` for a fixed
   projection and 31 depth values. `uInvProj` stays for `cull_lights.glsl`.
2. Pass `uDepthSize` as a uniform and derive the inverse in the shader. Remove
   both `textureSize` calls from `ssao.glsl`.
3. Add `SAO.MaxDistance` (default `48`) and `SAO.FadeRange` (default `8`).
   A non-positive or collapsed range is a hard cut.
4. The shader exposes `SAO.Power` (default `1.5`) and uses
   `pow(visibility, Power)` with a floored base.
5. In `ao()`, remove the depth rejection entirely. The original test compared
   the prepass texture against itself. A compare against `gl_FragCoord.z` is a
   coin flip, because the texture is 24-bit and the fragment depth is not. It
   dithers the AO and leaves bands. Remove `uDepth`, `uPreDepth`, and both
   binds.
6. The final rotation is a per-pixel white hash. A 4x4 noise table and
   interleaved gradient noise were both tried and removed.

Result: no per-sample matrix multiply, one less full-resolution fetch, and no
work on distant geometry. The estimator and reconstruction settle at the cost
table above, about 33 M fetches at `Medium`.

### Phase 3 — Settings

Implemented.

Engine settings in `SAOSettings`:

| Key | Type | Default |
|---|---|---|
| `SAO.Enabled` | bool | `true` |
| `SAO.Samples` | int | `8` |
| `SAO.ResolutionScale` | float | `0.5` |
| `SAO.Radius` | float | `0.35` |
| `SAO.Intensity` | float | `1.0` |
| `SAO.Bias` | float | `0.02` |
| `SAO.Power` | float | `1.5` |
| `SAO.MaxDistance` | float | `48.0` |
| `SAO.FadeRange` | float | `8.0` |
| `SAO.BlurPasses` | int | `2` |

Consumer settings in `WaywardBeyond.Client`. Follow the `BloomQuality` pattern
(`BloomQuality.cs`, `BloomQualityExtensions.cs`, `SettingsDefaults`):

- Add `AoQuality` with `Low`, `Medium`, `High`, `Ultra`, `Custom`.
- Map the presets onto `Samples`, `ResolutionScale`, and `MaxDistance`.
- Replace the `Settings.AoSamples` stepper with `Settings.AoQuality`. Keep the
  `Settings.AmbientOcclusion` toggle.
- Default to `AoQuality.Medium`.

| Preset | Samples | Scale | Max distance |
|---|---|---|---|
| `Low` | 4 | 0.25 | 32 |
| `Medium` | 8 | 0.5 | 48 |
| `High` | 16 | 0.5 | 64 |
| `Ultra` | 16 | 1.0 | no clip |

### Phase 4 — Verification

Open. It needs the target laptop.

There is no GPU timer in the engine. Measure one of these ways:

- RenderDoc, capture one frame per configuration, and read the SAO, depth
  downsample, and blur pass timings.
- Intel GPA or GPUView on the target laptop.
- A temporary `GL_ARB_timer_query` around the SAO block. Do not ship it.

Capture at 1080p in a scene with voxel geometry near the camera. Record five
configurations: SAO off, `Ultra`, `High`, `Medium`, and `Low`.

| Check | Expected |
|---|---|
| `Medium` pass cost | About 33 M fetches, 3.2x below the 104 M baseline |
| `Ultra` pass cost | Full resolution and no distance clip, about 4x `High` |
| Camera-facing wall | Flat AO, no lattice |
| Angled wall | No bands, the AO gradient follows the plane |
| Wall against sky | No dark rim, no halo wider than the blur |
| Voxel crease | Soft contact shading, no hard grid |
| Terrain ridge | AO follows the ridge, no staircase |
| Camera pan | No shimmer and no crawling pattern |

Run the same checks at 1.0 to separate the resolution from the estimator. Turn
on the F3 overlay and press F6 to cycle the SAO debug view through blurred, raw,
and depth. The overlay line names the active view.

## 6. Tests

- `Swordfish.Tests/SAOProjectionTests.cs` compares the new reconstruction
  against `Matrix4x4.Invert` over 31 depth values.
- `Swordfish.Tests/SettingsEntryTests.cs` covers the `AoQuality` presets,
  mirroring the bloom and shadow preset tests.
- The settings shell snapshot is regenerated.

The passes themselves need a GPU. Keep them behind the manual check above.

No GPU-free test covers the target size math, consistent with
`GetBloomLevelSize`.

## 7. Risks

- **Half resolution can shimmer** when the camera moves. The per-pixel white
  hash and the 9-tap blur reduce it. If it remains visible, add a temporal
  jitter of the hash. Do not add temporal reprojection in this change.
- **`MaxDistance` can pop** at the cutoff. The fade in Phase 2 covers it.
- **`R16f` linear filtering** requires no extension on desktop GL 4.3. Confirm
  on the oldest target driver. Fall back to `R32f` if the driver rejects it.
- **The AO blur must stay plain.** A depth weight preserves the voxel crease as
  a grid. The 9-tap stride-1 Gaussian is the final form.
- **The AO normal must never use a far-plane neighbor.** A sky neighbor
  produces a normal unrelated to the surface. Use the same-surface side.

## 8. Out of scope

- Replacing SAO with GTAO or HBAO.
- A depth-and-normal prepass that writes normals for SAO.
- Temporal reprojection of the AO buffer.

## 9. File map

| File | Change |
|---|---|
| `Swordfish/Graphics/SilkNET/OpenGL/Pipelines/ForwardPlusRenderingPipeline.cs` | Scaled SAO chain, shared blur, new uniforms, remove dead viewport |
| `Swordfish/Graphics/SilkNET/OpenGL/SAOProjection.cs` | New projection constants |
| `Swordfish/Graphics/SilkNET/OpenGL/TextureFormat.cs` | `R16f` |
| `Swordfish/assets/shaders/forward/ssao.glsl` | Estimator, same-surface normal, distance clip |
| `Swordfish/assets/shaders/depth_downsample.glsl` | Valid-tap AO depth downsample |
| `Swordfish/assets/shaders/ao_debug.glsl` | AO debug view |
| `Swordfish/assets/shaders/depth_debug.glsl` | Depth-edge debug view |
| `Swordfish/assets/shaders/pbr_plus.glsl` | Drop the redundant depth fetch in `ao()` |
| `Swordfish/Settings/SAOSettings.cs` | Samples, scale, radius, intensity, bias, power, distance, fade, blur passes |
| `Swordfish.Tests/SAOProjectionTests.cs` | New reconstruction test |
| `Swordfish.Tests/ConfigSerializationTests.cs` | New SAO defaults and migration test |
| `WaywardBeyond.Client/UI/Settings/AoQuality.cs` | New preset enum |
| `WaywardBeyond.Client/UI/Settings/AoQualityExtensions.cs` | Apply and detect presets |
| `WaywardBeyond.Client/UI/Settings/SettingsDefaults.cs` | `AO_QUALITY`, preset list |
| `WaywardBeyond.Client/UI/Settings/SettingsDraft.cs` | `AoQuality` field |
| `WaywardBeyond.Client/UI/Settings/DisplaySettingsCategory.cs` | Quality stepper |
| `WaywardBeyond.Client/UI/Layers/Menus/SettingsPage.cs` | Apply the preset |
| `WaywardBeyond.Client/assets/lang/en/ui.toml` | `aoQuality` label |
| `docs/specs/config-schemas.md` | New `SAO` keys |
| `docs/specs/settings-ui.md` | AO quality instead of AO samples |
| `docs/specs/rendering-sao.md` | New spec for the pass |
| `docs/README.md` | Index rows for this proposal and the spec |

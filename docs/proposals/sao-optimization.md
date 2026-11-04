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
| `Blur(ShaderProgram, DoubleBufferTarget, int passes)` at `ForwardPlusRenderingPipeline.cs:640-659` drives any blur program through a ping-pong chain | The SAO blur calls it. No new blur loop. |
| The Gaussian tap offsets are correct in `blur_gaussian.glsl:21-26` | Phase 1 has no shader edit. |
| `GetRenderSamples` and `CreateRenderTargets` allocate render targets from the anti-aliasing setting (`ForwardPlusRenderingPipeline.cs:614-628`) | The old 4× MSAA finding is closed. |

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
undefined behavior. Bloom now has the correct pattern at lines 640-659. SAO
still has the old one.

### 4.3 Each SAO sample rebuilds a view position with a matrix multiply

`ssao.glsl:26-31` multiplies a 4×4 matrix for every sample. The depth-to-view
conversion is a scale and a bias. Two uniforms cover it.

### 4.4 Normal reconstruction costs two more full-resolution taps

`ssao.glsl:88-93` reads two neighbor depths to build a normal. This is correct,
but it is 2.07 M extra fetches per frame. At half resolution it drops to
0.52 M.

### 4.5 The AO texture is `R32f`

`_ssaoTex` holds a value in 0..1. `R32f` costs 4 bytes per pixel and 8.3 MB at
full resolution.

### 4.6 The depth rejection test is a no-op

`pbr_plus.glsl:286-289` compares `texture(uDepth)` with `texture(uPreDepth)`.
`ShaderActivationCallback` binds the same `_depthTex` to both samplers
(`ForwardPlusRenderingPipeline.cs:963-967`). `step(aoDepth, fragDepth)` is
always 1. The test costs a fetch and rejects nothing. `gl_FragCoord.z` gives
the current fragment depth for free.

### 4.7 SAO covers the full scene

A radius of 0.1 view-space units at 60 m produces a sub-pixel offset. The pass
still spends full cost on distant geometry where the result is invisible.

## 5. Solution

### Phase 1 — Adopt the shared double-buffer path

Implemented.

1. Add `SAO.ResolutionScale` (default `0.5`) and `SAO_BLUR_PASSES = 2`.
2. Replace `_ssaoTex` and `_ssaoFBO` with one `DoubleBufferTarget _ssaoChain`.
   Size it at the scaled resolution with `R16f` and linear filtering. Add the
   format to `TextureFormat.cs`.
3. Draw the SAO pass into `_ssaoChain.FrontTarget`. Call
   `Blur(_blurShader, _ssaoChain, SAO_BLUR_PASSES)`. Use `DrawScreenQuad()`.
4. Bind `_ssaoChain.Front` at unit 5 in `ShaderActivationCallback`.
5. Resize the chain in `OnWindowResized`. Dispose it in `Dispose` in place of
   `_ssaoTex` and `_ssaoFBO`.
6. Delete the dead viewport calls in the SAO block.

The SAO pass writes the raw result into the chain front. `Blur` reads front and
writes back, then swaps. After an even pass count the result is back in
`Front`, which is exactly what the forward pass samples. The helper already
handles the swap, the uniform, and the shader bind.

Result at 1920×1080 with 8 samples:

| Stage | Texel fetches per frame |
|---|---|
| SAO pass at half resolution | 5.7 M |
| Blur, 2 passes × 9 taps at half resolution | 9.3 M |
| `ao()` in the forward pass | 6.2 M |
| **Total** | **≈ 21 M** |

This is about 4.9x less fetch traffic. Fill rate drops by 4x for the two SAO
passes.

The shared blur has no depth weights. If half-resolution AO bleeds across
silhouettes, add a dedicated `ssao_blur.glsl` with depth-weighted taps. The
`Blur` helper drives it without a pipeline change. Treat this as a conditional
step after the visual check.

### Phase 2 — Shader math

Implemented.

1. Replace the per-sample `UVToView` matrix multiply. Compute the view depth
   from the depth value with a scale and a bias. Compute `viewPos.xy` from the
   UV with a second scale. Pass both from the CPU.
   The engine uses a right-handed projection, so view Z stays negative. Keep
   the sign explicit.
   Pin the result with a unit test against `Matrix4x4.Invert` for a fixed
   projection and 32 depth values. `uInvProj` stays for `cull_lights.glsl`.
2. Pass `uInvSize` and `uAspect` as uniforms. Remove both `textureSize` calls
   from `ssao.glsl`.
3. Add `SAO.MaxDistance` (default `48`). Fade the result to `1.0` across the
   last 8 units.
4. Replace `pow(x, SAO_K)` with `x * sqrt(x)`. Remove the `SAO_K` uniform,
   because every preset uses 1.5.
5. In `ao()`, use `gl_FragCoord.z` instead of the `uDepth` fetch. Remove the
   `uDepth` sampler and its bind.
6. Index the noise table with `(pixel.x & 3) + ((pixel.y & 3) << 2)`.

Result: no per-sample matrix multiply, one less full-resolution fetch, and no
work on distant geometry. The total falls to about 19 M fetches, and less after
the distance clip.

### Phase 3 — Settings

Implemented.

Engine settings in `SAOSettings`:

| Key | Type | Default |
|---|---|---|
| `SAO.Enabled` | bool | `true` |
| `SAO.Samples` | int | `8` |
| `SAO.ResolutionScale` | float | `0.5` |
| `SAO.MaxDistance` | float | `48.0` |

Consumer settings in `WaywardBeyond.Client`. Follow the `BloomQuality` pattern
(`BloomQuality.cs`, `BloomQualityExtensions.cs`, `SettingsDefaults`):

- Add `AoQuality` with `Low`, `Medium`, `High`, `Custom`.
- Map the presets onto `Samples`, `ResolutionScale`, and `MaxDistance`.
- Replace the `Settings.AoSamples` stepper with `Settings.AoQuality`. Keep the
  `Settings.AmbientOcclusion` toggle.
- Default to `AoQuality.Medium`.

| Preset | Samples | Scale | Max distance |
|---|---|---|---|
| `Low` | 4 | 0.25 | 32 |
| `Medium` | 8 | 0.5 | 48 |
| `High` | 16 | 0.5 | 64 |

### Phase 4 — Verification

Open. It needs the target laptop.

There is no GPU timer in the engine. Use one of these to measure:

- RenderDoc, capture one frame per configuration. Read the SAO and blur pass
  timings.
- Intel GPA or GPUView on the target laptop.
- A temporary `GL_ARB_timer_query` around the SAO block. Do not ship it.

Measure four configurations: SAO off, `High`, `Medium`, and `Low`. Capture at
1080p in a scene with voxel geometry near the camera.

Check for visible quality loss at `Medium`. Look at contact shadows under a
wall, a step, and a fence. Check silhouettes against the sky for blur bleed.
Use a debug view that shows the AO buffer directly to judge the blur.

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

- **Half resolution can shimmer** when the camera moves. The frame-constant
  noise pattern reduces it. A depth-aware blur also helps. If it remains
  visible, add a temporal jitter of the noise pattern. Do not add temporal
  reprojection in this change.
- **`MaxDistance` can pop** at the cutoff. The fade in Phase 2 covers it.
- **`R16f` linear filtering** requires no extension on desktop GL 4.3. Confirm
  on the oldest target driver. Fall back to `R32f` if the driver rejects it.
- **The shared blur has no depth weights.** See the conditional step in
  Phase 1. Judge it against silhouettes before accepting it.

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
| `Swordfish/assets/shaders/forward/ssao.glsl` | Linear depth reconstruction, uniforms, distance clip |
| `Swordfish/assets/shaders/pbr_plus.glsl` | Drop the redundant depth fetch in `ao()` |
| `Swordfish/Settings/SAOSettings.cs` | `ResolutionScale`, `MaxDistance` |
| `Swordfish.Tests/SAOProjectionTests.cs` | New reconstruction test |
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

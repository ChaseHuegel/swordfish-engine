# Scalable Ambient Obscurance

SAO renders contact shadows in the forward+ OpenGL pipeline. The pass reads the
depth prepass, evaluates obscurance at a scaled resolution, blurs through a
ping-pong chain, and multiplies the result into the shaded color.

Code: `Swordfish/Graphics/SilkNET/OpenGL/Pipelines/ForwardPlusRenderingPipeline.cs:421-501`.

## Pass order

`PreRender` runs the depth prepass, then the SAO block, then tile light culling
and the forward pass.

1. When `ResolutionScale` is below 1, average the quadrant centres of the source
   block that are below the far plane into `_ssaoDepthFBO` at the AO resolution
   (`depth_downsample.glsl`). Window depth is linear across a plane, so the mean
   equals the depth at the block centre. A point sample turns an angled plane
   into a staircase and the AO reconstructs that staircase as banding. A sky tap
   is the far plane and is ignored, because mixing it into the block puts the
   reconstructed point on neither surface. A block with no valid tap stays at
   the far plane. At scales below 0.5 each tap is one source texel, so the
   result is a 4-point average, not a full block average. It is exact for a
   plane.
2. Draw the SAO result into `DoubleBufferTarget.FrontTarget`. The target is
   sized from `SAO.ResolutionScale`.
3. Blur the chain with `Blur(_blurShader, _ssaoChain, SAO.BlurPasses)`.
   Each separable pass reads front and writes back, then swaps. The result ends
   in `Front` for any pass count. Two passes are the default.
4. Bind `_ssaoChain.Front` to texture unit 5 in `ShaderActivationCallback`
   (`ForwardPlusRenderingPipeline.cs:1113`).

The blur is a separable Gaussian with a 9-tap kernel at a stride of 1 AO texel.
The stride must be 1: a larger stride under-samples the per-pixel rotation and
leaves a period-3 grid. The paper's stride-3 form only applies after its 2x2
bilateral reconstruction pass, which this pipeline does not have.

The forward pass samples the AO texture with bilinear filtering. The scaled
resolution is hidden by the blur and the filter.

## Shader

`Swordfish/assets/shaders/forward/ssao.glsl`:

- `ViewPosition` (`:28`) reconstructs a view position from screen UV and depth
  with `uViewScale`, `uDepthA`, and `uDepthB`. The constants come from
  `SAOProjection` (`Swordfish/Graphics/SilkNET/OpenGL/SAOProjection.cs`). No
  matrix multiply runs per sample.
- `SampleDepthBilinear` (`:36`) interpolates four depth texels with `texelFetch`
  so angled surfaces do not staircase. If any of the four texels is at the far
  plane, it returns `1.0` so silhouette samples do not blend sky depth with
  foreground geometry.
- The normal is rebuilt from four neighbor depth taps (`:85`). Each axis uses
  the neighbor that stays on this surface. A far-plane neighbor and a crease
  neighbor both lose to the same-surface side. A pixel with no valid neighbor
  on an axis receives no AO.
- Obscurance uses the plastic-constant spiral. The sample radius is
  projection-scaled with `uRadiusScale` (the HPG12 `projScale`), confined to a
  world-space `uRadius` with a cubic falloff. The projection divide uses view
  depth (`-verPos.z`), not the Euclidean distance, so the radius does not shrink
  toward the screen corners.
- The obscurance term is `max(0, dot(v, n) - uBias)` over `dot(v, v) + 0.02`,
  normalized by `uSampleCount`. Sky samples contribute zero occlusion and still
  count toward the denominator so silhouettes against the sky do not darken.
  The bias suppresses self-occlusion on sloped surfaces. Keep it constant: a
  distance-scaled bias removes all occlusion on distant surfaces.
- The rotation must stay a per-pixel white hash. See the invariants under
  [Cost](#cost).
- `uSampleCount` is `SAO.Samples`.
- `uMaxDistance` clips the pass. `uFadeRange` fades the result to 1.0 over the
  last meters. A non-positive range, or a range that collapses below float
  precision at a very large `MaxDistance`, is a hard cut.
- `uIntensity` scales the occlusion. `uPower` is the exponent of the visibility
  curve. The base is floored before `pow`, so a zero or negative exponent is
  defined.

`WaywardBeyond.Client/assets/shaders/lightedArray.glsl:44` applies the result:

```glsl
vec4 color = max(albedo * VertexColor * shade(albedo.rgb) * ao(albedo), emissive);
```

`shade()` returns ambient plus direct light, so this multiplies both. The
current behavior darkens direct light in creases.

Do not gate this with a depth test. The prepass depth is a 24-bit texture value.
`gl_FragCoord.z` is full precision. Comparing them with `step()` is a coin flip
at equal depth. It dithers the AO per pixel and leaves horizontal bands where
the whole AO is rejected. The prepass and the forward pass rasterize the same
surface, so the test cannot separate a surface from itself.

## Settings

`Swordfish/Settings/SAOSettings.cs`:

| Key | Type | Default | Notes |
|---|---|---|---|
| `Enabled` | bool | `true` | |
| `Samples` | int | `8` | cost is linear, not capped |
| `ResolutionScale` | float | `0.5` | AO buffer as a fraction of the screen, at least 1 texel, above 1 supersamples |
| `Radius` | float | `0.35` | meters, `0` disables, a negative radius mirrors the disk |
| `Intensity` | float | `1.0` | |
| `Bias` | float | `0.02` | meters |
| `Power` | float | `1.5` | visibility exponent |
| `MaxDistance` | float | `48.0` | meters |
| `FadeRange` | float | `8.0` | meters, `0` and a collapsed range are hard cuts |
| `BlurPasses` | int | `2` | 9 taps per pass, not capped |

The estimator defaults (`Radius`, `Intensity`, `Bias`, `Power`) follow the HPG12
reference. The reconstruction is the plain 9-tap stride-1 blur described above.
Only `ResolutionScale` has a range guard: the buffer is at least one texel. No
other value has a range check. The shader guards the math where a value could be
undefined: a zero or underflowed radius returns no occlusion, `pow` uses a base
floor, and the fade uses `smoothstep` only for a positive interval that has not
collapsed.

`WaywardBeyond.Client` maps `AoQuality` presets onto the performance values in
`AoQualityExtensions.cs`:

| Preset | Samples | Scale | Max distance |
|---|---|---|---|
| `Low` | 4 | 0.25 | 32 |
| `Medium` | 8 | 0.5 | 48 |
| `High` | 16 | 0.5 | 64 |
| `Ultra` | 16 | 1.0 | no clip |

`Ultra` sets `MaxDistance` to `float.MaxValue`, so the distance clip never
triggers. The far plane does not affect the preset. The presets own only
`Samples`, `ResolutionScale`, and `MaxDistance`. `Radius`, `Intensity`, `Bias`,
`Power`, `FadeRange`, and `BlurPasses` stay independent, so applying a preset
does not wipe an art tweak.

A change to the scale resizes the chain on the next frame. A window resize
resizes it through `OnWindowResized`. The default is half resolution.

## Debug view

Set `DebugSettings.SAO` to true at runtime. The debug flags live on
`Swordfish.Settings.DebugSettings`, not in `render.toml`. `PostRender` writes the
blurred AO chain front to the back buffer and returns before bloom
(`ForwardPlusRenderingPipeline.cs:598-610, 807-822`). The image is grayscale: 1 is
unoccluded and 0 is fully occluded.

While the F3 debug overlay is open, F6 cycles the view through blurred AO, raw
AO, depth edges, and off. The overlay prints the active view when it is not
off. F3 clears the SAO debug state.

Use it to separate an AO defect from the brick shading. The debug image contains
only the AO value, so a streak that appears there is AO. A streak that appears
in the scene but not in the debug image comes from the albedo, the normal map,
or the baked light.

Set `DebugSettings.SAORaw` with `SAO` to skip the blur and show the raw pass
output. It has no effect while `SAO` is off. Compare the two debug
images: a tile in the raw image and a tile in the blurred image means the blur
is not removing it. A clean raw image and a tile in
the blurred image means the blur is adding it.

Set `DebugSettings.SAODepth` with `SAO` to show the depth-edge magnitude instead
of AO. White marks a depth step larger than one centimetre between
neighbouring pixels. Use it to tell whether an AO line follows real depth relief
or is generated by the AO pass.

## Cost

At 1920×1080 with `Medium` the pass issues about 33 M texel fetches per frame.
An AO pixel costs 37 fetches (4 per sample for the depth, 1 for the centre, and
4 for the normal). The depth downsample costs 4 taps per AO pixel. The blur
costs 9 taps per pass, two passes by default. The composite costs one fetch per
full-resolution pixel.

`MaxDistance` is a coverage optimization. The early-out drops an AO pixel beyond
the clip to one depth fetch. The downsample and the blur still run across the
whole buffer, so the saving is the estimator term, not the whole pass.

`Samples`, `BlurPasses`, and `ResolutionScale` are not capped. Cost is linear in
the first two. A scale above 1 supersamples the pass and is bounded only by the
texture allocation.

The clip and the fade use the Euclidean distance to the camera. The obscurance
radius uses view depth. The two differ at the screen edges.

The pass was the largest post-process cost on integrated GPUs before the
rework. Keep these invariants:

- The SAO chain is one `DoubleBufferTarget`, not two separate textures in one
  framebuffer. A pass never samples its own draw target.
- The default `ResolutionScale` is 0.5. The buffer is at least one texel, and a
  non-positive or non-finite scale maps to one texel.
- The vertex buffer and quad drawing go through `DrawScreenQuad`.
- The AO blur uses `WEIGHTS[0]` for the center tap. A center weight of `1.0`
  narrows the kernel.
- The AO blur stride is 1 AO texel. A larger stride under-samples the per-pixel
  rotation and creates a period-3 grid.
- The AO blur may run any number of passes. Each pass writes `Back` and then
  swaps, so the result is in `Front` after every pass, not only even counts.
- Both `_depthTex` and `_ssaoDepthTex` use `ClampNearest` so normal
  reconstruction and light culling read exact texels. The shader does its own
  bilinear `texelFetch` interpolation for spiral taps and rejects any tap that
  touches the far plane (`>= 1.0`) before interpolating. Window depth is linear
  across a plane, so this keeps a sample on the surface without blending sky
  depth into silhouettes. Nearest sampling leaves a sub-texel error that bands
  angled surfaces and vanishes on camera-facing ones.
- The auxiliary AO depth stays at 1x1 while the screen depth is bound. A full
  AO-sized `R32F` allocation is idle at scales of 1 and above.
- The AO depth downsample must **average the valid quadrant centres**, not point
  sample them. A point sample staircases angled planes and the AO reconstructs
  the staircase as banding. A sky tap is the far plane and is ignored. If no tap
  is valid, the block writes 1.0. The taps are offset by `0.25 / aoSize` in
  source UV so they stay symmetric around the block centre for any scale. At
  scales below 0.5 each tap is one source texel, so the result is a 4-point
  average, exact for a plane.
- A zero or underflowed radius, or a non-positive sample count, must return no
  occlusion. The falloff divides by `radius * radius` and the estimator divides
  by `uSampleCount`, so zero values would produce undefined math.
- The fade is active only when `FadeRange > 0` and
  `MaxDistance - FadeRange < MaxDistance`. Otherwise the pass uses a hard cut.
  The subtraction can collapse below float precision at a very large
  `MaxDistance`.
- The AO normal must never use a far-plane neighbor. A sky neighbor maps to the
  far plane, and its position produces a normal unrelated to the surface. Use
  the same-surface neighbor on each axis.
- The AO screen radius divides by view depth (`-verPos.z`), not by the
  Euclidean distance to the camera. The perspective divide scales with z, so
  the distance form shrinks the radius toward the screen corners.
- The AO rotation is a per-pixel white hash. Do not use a 4x4 tile or
  interleaved gradient noise: both have a spatial period (~16 px and ~15 px)
  that reads as banding once the AO buffer is at a reduced scale.

## Tests

- `Swordfish.Tests/SAOProjectionTests.cs` compares `SAOProjection` against
  `Matrix4x4.Invert` for 31 depth values.
- `Swordfish.Tests/SettingsEntryTests.cs` covers preset apply and detect,
  including that presets leave the art knobs alone and that Ultra follows the
  far plane.
- `Swordfish.Tests/ConfigSerializationTests.cs` covers the new defaults and the
  migration from a config that predates the new keys.
- `Swordfish.Tests/SettingsShellViewTests.cs` pins the settings page layout.

The pass itself needs a GPU. Verify it with a RenderDoc capture on the target
hardware. See [the proposal](../proposals/sao-optimization.md).

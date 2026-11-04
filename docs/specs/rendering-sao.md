# Scalable Ambient Obscurance

SAO renders contact shadows in the forward+ OpenGL pipeline. The pass reads the
depth prepass, evaluates obscurance at a scaled resolution, blurs through a
ping-pong chain, and multiplies the result into the shaded color.

Code: `Swordfish/Graphics/SilkNET/OpenGL/Pipelines/ForwardPlusRenderingPipeline.cs:393-426`.

## Pass order

`PreRender` runs the depth prepass, then the SAO block, then tile light culling
and the forward pass.

1. Draw the SAO result into `DoubleBufferTarget.FrontTarget`. The target is
   sized from `SAO.ResolutionScale`.
2. Blur the chain with `Blur(_blurShader, _ssaoChain, SAO_BLUR_PASSES)`. Two
   separable passes read front and write back, then swap. The result ends in
   `Front`.
3. Bind `_ssaoChain.Front` to texture unit 5 in `ShaderActivationCallback`
   (`ForwardPlusRenderingPipeline.cs:975`).

The forward pass samples the AO texture with bilinear filtering. The scaled
resolution is hidden by the blur and the filter.

## Shader

`Swordfish/assets/shaders/forward/ssao.glsl`:

- `ViewPosition` (`:31`) reconstructs a view position from screen UV and depth
  with `uViewScale`, `uDepthA`, and `uDepthB`. The constants come from
  `SAOProjection` (`Swordfish/Graphics/SilkNET/OpenGL/SAOProjection.cs`). No
  matrix multiply runs per sample.
- The normal is rebuilt from two neighbor depth taps (`:100-104`).
- Obscurance uses the plastic-constant spiral and a 4x4 noise rotation. The
  radius is in view-space units.
- `uSampleCount` is `SAO.Samples`.
- `uMaxDistance` clips the pass and `uMaxDistanceFade` fades the result to 1.0
  over the last meters.
- The exponent is `pow(x, 1.5)` written as `x * sqrt(x)`.

`Swordfish/assets/shaders/pbr_plus.glsl:276-291` applies the result:

```glsl
float visibility = step(texture(uPreDepth, screenUV).r, gl_FragCoord.z);
float ao = mix(1.0, texture(uAO, screenUV).r, albedo.a * visibility);
```

`uPreDepth` is the depth prepass. `gl_FragCoord.z` is the current fragment
depth. The test rejects AO where the fragment is not the prepass surface.

## Settings

`Swordfish/Settings/SAOSettings.cs`:

| Key | Type | Default | Range |
|---|---|---|---|
| `Enabled` | bool | `true` | |
| `Samples` | int | `8` | presets use 4, 8, or 16 |
| `ResolutionScale` | float | `0.5` | clamped to 0.25-1 |
| `MaxDistance` | float | `48.0` | meters |

`WaywardBeyond.Client` maps `AoQuality` presets onto these values in
`AoQualityExtensions.cs`:

| Preset | Samples | Scale | Max distance |
|---|---|---|---|
| `Low` | 4 | 0.25 | 32 |
| `Medium` | 8 | 0.5 | 48 |
| `High` | 16 | 0.5 | 64 |

The engine clamps `ResolutionScale`. A change to the scale resizes the chain on
the next frame. A window resize resizes it through `OnWindowResized`.

## Cost

At 1920×1080 with `Medium` the pass issues about 21 M texel fetches per frame.
An SAO pixel costs 11 fetches. The blur costs 9 fetches per pass at the scaled
resolution. The composite costs 2 fetches per full-resolution pixel.

The pass was the largest post-process cost on integrated GPUs before the
rework. Keep these invariants:

- The SAO chain is one `DoubleBufferTarget`, not two separate textures in one
  framebuffer. A pass never samples its own draw target.
- The SAO buffer is smaller than the screen unless `ResolutionScale` is 1.
- The vertex buffer and quad drawing go through `DrawScreenQuad`.

## Tests

- `Swordfish.Tests/SAOProjectionTests.cs` compares `SAOProjection` against
  `Matrix4x4.Invert` for 31 depth values.
- `Swordfish.Tests/SettingsEntryTests.cs` covers preset apply and detect.
- `Swordfish.Tests/SettingsShellViewTests.cs` pins the settings page layout.

The pass itself needs a GPU. Verify it with a RenderDoc capture on the target
hardware. See [the proposal](../proposals/sao-optimization.md).

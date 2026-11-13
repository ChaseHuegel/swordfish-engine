# Bug: Output applies no color transfer and no tonemapping

- Type: bug
- Status: closed
- Workflow: ../specs/issues.md

## Problem

The renderer never applies a color transfer function, and it never tonemaps.
Every stage treats the value as final, so lighting math runs on gamma-encoded
data and the output is clipped instead of mapped.

The path has three inconsistent conventions at once:

- Textures load as plain `Rgba` (`Swordfish/Graphics/SilkNET/OpenGL/TextureFormat.cs`),
  so an sRGB albedo PNG is sampled without decoding. Shading multiplies those
  encoded values.
- The scene renders into `Rgba16f` attachments
  (`Swordfish/Graphics/SilkNET/OpenGL/Pipelines/ForwardPlusRenderingPipeline.cs`),
  which hold linear-looking numbers that were never linearized.
- The final blit writes to the back buffer with no encode. Nothing enables
  `EnableCap.FramebufferSrgb`, and no shader raises values to `1 / 2.2`.

Additive passes make this visible. Bloom and the skybox add encoded values, so a
halo brightens faster than the light it represents, and any sum above 1.0 clips
per channel. Clipping per channel shifts hue, so a bright cyan glow turns white
instead of staying cyan. Raising bloom intensity exposes it immediately.

## Consequence

- Lighting falloff is wrong. Illumination is multiplied in gamma space, so
  mid-tones sit at the wrong brightness.
- Additive and blended effects composite incorrectly. This covers bloom,
  billboards, and the emissive overlay.
- Anything above 1.0 clips. There is no tonemap to roll off highlights.
- The difference between bright and dim emissive material cannot grow, because
  the 8-bit bloom attachment clamps at 1.0 before the blur.

## Acceptance criteria

- [x] The repository records one color-space contract: which stage holds linear
      values and where the transfer function is applied.
- [x] The contract is applied exactly once on the way to the back buffer, and
      texture sampling decodes sRGB sources.
- [x] Lighting and additive effects composite in linear space.
- [x] A tonemap stage maps values above 1.0 into gamut before the encode, so
      bright emissive surfaces keep their hue.
- [x] A reference scene compares against a captured baseline at each step.

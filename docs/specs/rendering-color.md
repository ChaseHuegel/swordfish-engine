# Color Transfer and Tonemapping

The forward+ OpenGL pipeline shades and composites in linear HDR space, maps
highlights into gamut with Khronos PBR Neutral, and applies the IEC 61966-2-1
sRGB transfer function once when writing to the back buffer.

## Color-Space Contract

| Stage / Resource | Color Space | Format | Transfer Rule |
|---|---|---|---|
| Color textures (albedo, emissive, skybox, 2D icons) | sRGB | `Srgb8Alpha8` | Hardware decodes sRGB to linear on sample. Alpha stays linear. |
| Data textures (normal, metallic, smoothness, MSDF atlas) | Linear | `Rgba8` | Sampled without conversion. |
| Scene color (`_colorRBO`, `_colorResolveTex`) | Linear HDR | `Rgba16f` | Direct light, ambient light, shadows, SAO, skybox, and alpha-blended meshes composite in linear HDR space. |
| Emissive source (`_bloomRBO`, `_bloomResolveTex`) | Linear HDR | `R11fG11fB10f` | Scene shaders write linear HDR emissive values to `BrightColor` without clamping at `1.0`. |
| Bloom mip chain (`_bloomChain[0..3]`) | Linear HDR | `Rgba16f` | Extracts, blurs, and accumulates in linear HDR space. Alpha holds the linear non-emissive mask. |
| Post-process composite (`bloom.glsl` to `FBO 0`) | Linear HDR to sRGB | Back buffer | Adds bloom to scene color in linear HDR, tonemaps into `[0, 1]`, and encodes linear to sRGB once. |
| Screen-space UI (`ForwardRenderingPipeline`) | sRGB | Back buffer | Vertex colors and MSDF coverage draw in sRGB screen space. UI textured shaders re-encode sampled linear RGB to sRGB before multiplying by vertex color. |

## Texture Decoding and UI Encoding

`Texture`, `TextureArray`, and `TextureCubemap` carry a `ColorSpace` property
(`ColorSpace.Srgb` or `ColorSpace.Linear`). The default is `ColorSpace.Srgb`.

- `GLRenderContext.BindTexture` and `ForwardPlusRenderingPipeline.ApplySkybox`
  upload `ColorSpace.Srgb` textures as `TextureFormat.Srgb8Alpha8` and
  `ColorSpace.Linear` textures as `TextureFormat.Rgba` (`InternalFormat.Rgba8`).
- `PBRTextureArraysParser` sets `ColorSpace.Srgb` on the `bricks_albedo` and
  `bricks_emissive` arrays. It sets `ColorSpace.Linear` on `bricks_metallic`,
  `bricks_smoothness`, and `bricks_normal`.
- `ReefRenderer` packs the MSDF typeface bitmaps and solid white region into
  `ui_atlas` with `ColorSpace.Linear` so glyph distance fields are not decoded
  as color.
- Textures in `TextureDatabase` are uploaded as `Srgb8Alpha8` so 3D world
  draws sample linear color. Screen-space textured shaders
  (`ui_reef_textured.glsl`, `ui_default.glsl`, `ui/crosshair.glsl`) write
  directly to the back buffer after `ForwardPlusRenderingPipeline.PostRender`.
  Each shader calls `linearToSrgb(tex.rgb)` before multiplying by `VertexColor`
  so untinted UI textures preserve their authored sRGB values.

## Post-Process Composite and Tonemapping

`ForwardPlusRenderingPipeline.PostRender` runs the output path:

1. Blit `ColorAttachment0` (`_colorRBO`) into `_colorResolveFBO` (`Rgba16f`)
   with `BlitFramebufferFilter.Nearest`.
2. When `Bloom.Enabled` is true and `Bloom.Intensity` is positive:
   - Blit `ColorAttachment1` (`_bloomRBO`, `R11fG11fB10f`) into
     `_bloomResolveFBO` (`R11fG11fB10f`) with `BlitFramebufferFilter.Nearest`.
   - Extract into `_bloomChain[0].FrontTarget` with `bloom_extract.glsl`. The
     emissive mask tests `MIN_EMISSIVE = 1.0 / (255.0 * 12.92)`, which matches
     the smallest non-zero 8-bit sRGB texel after linear decode.
   - Blur each active level and accumulate coarser levels back into
     `_bloomChain[0].FrontTarget`.
3. Bind the default framebuffer (`FBO 0`) with blending disabled and draw
   `bloom.glsl` once over the screen quad:
   - Sample `_colorResolveTex` and add `bloom.rgb * bloom.a * uIntensity` in
     linear HDR space when `uIntensity > 0`.
   - Map linear HDR into `[0, 1]` with `tonemapPbrNeutral`.
   - Encode linear `[0, 1]` to sRGB with the piecewise IEC 61966-2-1 curve
     `linearToSrgb`.

`ColorTransferMath` provides the matching CPU implementation of `SrgbToLinear`,
`LinearToSrgb`, and `TonemapPbrNeutral`.

## Source of Truth

- Color space enum: `Swordfish/Graphics/ColorSpace.cs:6-17`
- CPU transfer and tonemap math: `Swordfish/Graphics/ColorTransferMath.cs:7-65`
- OpenGL texture formats: `Swordfish/Graphics/SilkNET/OpenGL/TextureFormat.cs:5-21`
- Texture asset types: `Swordfish/Graphics/Texture.cs:5-25`, `Swordfish/Graphics/TextureArray.cs:5-12`, `Swordfish/Graphics/TextureCubemap.cs:3-8`
- Texture upload binding: `Swordfish/Graphics/SilkNET/OpenGL/GLRenderContext.cs:198-235`
- Reef UI atlas color space: `Swordfish/Graphics/SilkNET/OpenGL/Renderers/ReefRenderer.cs:103-105`
- Forward+ resolve, bloom, and composite: `Swordfish/Graphics/SilkNET/OpenGL/Pipelines/ForwardPlusRenderingPipeline.cs:71-75, 261-265, 352-353, 618-698, 743-746, 1244-1253`
- Bloom extraction shader: `Swordfish/assets/shaders/bloom_extract.glsl:14-29`
- Composite, tonemap, and encode shader: `Swordfish/assets/shaders/bloom.glsl:14-56`
- UI textured shaders: `Swordfish/assets/shaders/ui_reef_textured.glsl:38-55`, `Swordfish/assets/shaders/ui_default.glsl:10-22`, `WaywardBeyond.Client/assets/shaders/ui/crosshair.glsl:10-22`
- Client PBR texture arrays: `WaywardBeyond.Client/Graphics/PBRTextureArraysParser.cs:79-85`

## Tests That Pin This

- `Swordfish.Tests/ColorTransferMathTests.cs:8-105`

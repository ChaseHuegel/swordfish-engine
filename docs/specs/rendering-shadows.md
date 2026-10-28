# Dynamic Shadows

Dynamic shadows render directional cascaded shadow maps (CSM) and omnidirectional point light shadows in the Forward+ OpenGL renderer.

## Pipeline Flow

1. Forward+ stages lights and camera state in `ForwardPlusRenderingPipeline`.
2. `RenderShadowPass` runs before the main forward lighting pass.
3. If shadows are enabled, the pipeline renders depth into two textures:
   - 2D texture array (`GL_TEXTURE_2D_ARRAY`) for directional light cascades.
   - Cubemap array (`GL_TEXTURE_CUBE_MAP_ARRAY`) for omnidirectional point lights.
4. Renderers run a fast depth-only pass (`GLInstancedRenderer.RenderShadowPass`).
5. Objects with `RenderOptions.CastShadows = false` are skipped in shadow passes.
6. The main forward lighting pass binds shadow textures and evaluates shadow factors in `pbr_plus.glsl`.
7. Objects with `RenderOptions.ReceiveShadows = false` skip shadow attenuation.

## Directional Light Shadows (CSM)

Directional shadows use Cascaded Shadow Maps with 1 to 4 split cascades.
`ShadowCascadeHelper` computes split distances using practical split scheme logarithmic and linear blending.
Each cascade projects onto the camera sub-frustum with texel snapping to prevent edge shimmer during camera motion.
The shader selects the cascade from view-space depth. It samples the 2D texture array with hardware depth comparison.

## Point Light Shadows

Omnidirectional point lights cast shadows via a cube map array texture (`TexCubeArray`).
The pipeline gathers point lights with `LightComponent.CastShadows = true`.
It sorts candidates by distance to the active camera.
The closest candidates up to `ShadowSettings.MaxPointShadows` render into array cube slices.
Each cube slice draws six 90-degree perspective faces with a shared depth framebuffer.
In the lighting pass, shaded fragments compute the point light distance and sample the cubemap array.

## Filtering Modes

`ShadowFilterMode` controls filtering in `pbr_plus.glsl`:
- `Sharp`: 1-tap hardware depth comparison texture fetch.
- `Soft`: 9-tap percentage-closer filtering (PCF) with normal-based depth bias to eliminate acne.

## Quality Presets

`WaywardBeyond.Client` defines quality presets that map to `ShadowSettings`:

| Preset | Resolution | Cascades | Max Distance | Max Point Lights | Filter Mode |
|---|---|---|---|---|---|
| `Off` | 0 | 0 | 0 | 0 | `Sharp` |
| `Low` | 512 | 2 | 48m | 2 | `Sharp` |
| `Medium` | 1024 | 3 | 96m | 3 | `Soft` |
| `High` | 2048 | 4 | 160m | 4 | `Soft` |
| `Ultra` | 4096 | 4 | 256m | 8 | `Soft` |

## Settings and Components

- `ShadowSettings`: settings for resolution, distance, cascade counts, biases, and filter mode.
- `RenderSettings.Shadows`: global shadow configuration container.
- `RenderOptions.CastShadows`: controls whether a mesh draws into shadow depth maps.
- `RenderOptions.ReceiveShadows`: controls whether a mesh samples shadow maps during shading.
- `RenderOptions.AlphaMode`: controls alpha behavior: `Opaque` (fast-path depth write), `Masked` (alpha cutout with `discard`), or `Blend` (skips shadow pass).
- `RenderOptions.AlphaCutoff`: alpha threshold for cutout shadow discarding (default 0.5).
- `DirectionalLightComponent`: stores directional light direction, color, intensity, and `CastShadows`.
- `LightComponent.CastShadows`: toggles point light participation in point shadow allocation.

## Source of Truth

- Presets: `WaywardBeyond.Client/UI/Settings/ShadowQuality.cs:6-15`
- Filter modes: `Swordfish/Settings/ShadowFilterMode.cs:4-8`
- Settings: `Swordfish/Settings/ShadowSettings.cs:6-17`
- Global settings: `Swordfish/Settings/RenderSettings.cs:22`
- Mesh options: `Swordfish/Graphics/RenderOptions.cs:8-14`
- Alpha mode: `Swordfish/Graphics/AlphaMode.cs:4-9`
- Directional light: `Swordfish/ECS/DirectionalLightComponent.cs:6-20`
- Point light: `Swordfish/ECS/LightComponent.cs:6-20`
- Cascade math: `Swordfish/Graphics/SilkNET/OpenGL/ShadowCascadeHelper.cs:6-143`
- Cubemap array texture: `Swordfish/Graphics/SilkNET/OpenGL/TexCubeArray.cs:6-94`
- Shadow pass execution: `Swordfish/Graphics/SilkNET/OpenGL/Pipelines/ForwardPlusRenderingPipeline.cs:576-785`
- Depth draw pass: `Swordfish/Graphics/SilkNET/OpenGL/Renderers/GLInstancedRenderer.cs:177-276`
- Shader evaluation: `Swordfish/assets/shaders/pbr_plus.glsl:132-227`
- Client sun bootstrap: `WaywardBeyond.Client/Entry.cs:38-43`
- Client UI controls: `WaywardBeyond.Client/UI/Settings/DisplaySettingsCategory.cs:105-113`

## Tests That Pin This

- `Swordfish.Tests/ShadowCascadeHelperTests.cs:10-165`
- `Swordfish.Tests/SettingsEntryTests.cs:49-98`
- `Swordfish.Tests` full engine suite.

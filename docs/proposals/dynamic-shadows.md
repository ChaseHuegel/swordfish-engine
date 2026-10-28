# Dynamic Shadows — Proposal and Development Plan

Status: implemented.

Scope: the engine rendering tier (`Swordfish`), the forward+ OpenGL pipeline, the shader library, and the consumer integration in `WaywardBeyond.Client`.

## 1. Summary

The Swordfish engine has a forward+ rendering pipeline with compute-driven tile light culling, Screen-Space Ambient Occlusion (SAO), and bloom. The engine has no dynamic shadow mapping system. All lights illuminate surfaces through walls and terrain voxels. This lack of occlusion breaks spatial depth, visual grounding, and interior darkness.

This proposal introduces a performant dynamic shadow system into Swordfish. The system targets the existing OpenGL 4.3 core forward+ pipeline. The design prioritizes performance, predictable memory usage, and zero runtime garbage collection over state-of-the-art cinematic fidelity.

The solution provides:
1. Cascaded Shadow Maps (CSM) for directional lighting (sun and moon).
2. A strictly budgeted shadow map array for prominent local point lights.
3. Both sharp (1-tap hard comparison) and soft (bilinear Percentage-Closer Filtering) shadow filtering.
4. Three tiers of configuration: global quality settings, per-light casting toggles, and per-mesh/entity casting and receiving toggles.
5. First-class API consumer integration in `WaywardBeyond.Client` for voxel chunks, entities, billboards, viewmodels, and game settings.

## 2. Current state

### 2.1 The Forward+ Pipeline

The active pipeline is `ForwardPlusRenderingPipeline<TRenderStage>` in `Swordfish/Graphics/SilkNET/OpenGL/Pipelines/ForwardPlusRenderingPipeline.cs:18`.

The current frame flow executes in three phases:
- `PreRender` (`ForwardPlusRenderingPipeline.cs:273-457`):
  - Renders an optional half-resolution depth pre-pass for SAO (`_preDepthFBO`).
  - Evaluates SAO into `_ssaoFBO` and applies horizontal/vertical blur passes.
  - Renders a full-resolution depth pass into `_depthFBO` using `forward/depth.glsl`.
  - Collects point lights from ECS via `WriteLightAction` (`ForwardPlusRenderingPipeline.cs:576-586`) and uploads them to `_lightsSSBO` (`GPULight`).
  - Clears `_tileCountsSSBO` and dispatches `forward/cull_lights.glsl` compute shader (`ForwardPlusRenderingPipeline.cs:384-400`).
  - Renders skybox or ambient background to `_renderFBO`.
- `Draw` (`RenderPipeline.cs:29-43`):
  - Iterates render stages (`GLInstancedRenderer`, `GLLineRenderer`).
  - `GLInstancedRenderer.Render` (`GLInstancedRenderer.cs:70-92`) renders opaque instances, then transparent instances sorted back-to-front.
  - Executes `ForwardPlusRenderingPipeline.ShaderActivationCallback` (`ForwardPlusRenderingPipeline.cs:534-551`), binding tile uniforms and depth/SSAO textures.
- `PostRender` (`ForwardPlusRenderingPipeline.cs:459-532`):
  - Blits `_renderFBO` color attachment 0 to the backbuffer.
  - Evaluates multi-pass bloom blur and blends the result over the screen.

### 2.2 Current Shading and Lighting Model

Mesh rendering currently uses `Swordfish/assets/shaders/pbr_plus.glsl`:
- Lights are defined as `GPULight` (`GPULight.cs:7-17`):
  ```csharp
  public Vector4 PosRadius;  // x, y, z, radius
  public Vector4 ColorSize;  // r, g, b, size
  ```
- The fragment shader loops over lights assigned to the current tile (`pbr_plus.glsl:128-140`):
  ```glsl
  for (uint i = 0u; i < count; ++i) {
      uint lightIndex = indices[base + i];
      if (lightIndex < lights.length()) {
          Light light = lights[lightIndex];
          color += EvalLight(light, vNormal, V, F0, albedo, Roughness, Metallic);
      }
  }
  ```
- No shadow test exists. Occlusion depends entirely on SSAO (`pbr_plus.glsl:145-161`), which provides only localized contact shading.

### 2.3 Render Options and ECS Components

Meshes and entities hold render state via:
- `MeshRenderer` (`Swordfish/Graphics/MeshRenderer.cs:5-45`) holding `RenderOptions`:
  ```csharp
  public struct RenderOptions
  {
      public bool DoubleFaced;
      public bool Wireframe;
      public bool IgnoreDepth;
  }
  ```
- `MeshRendererComponent` (`Swordfish/Graphics/MeshRendererComponent.cs:5-10`), binding an entity to a `MeshRenderer`.
- `LightComponent` (`Swordfish/ECS/LightComponent.cs:5-17`):
  ```csharp
  public struct LightComponent : IDataComponent
  {
      public float Radius;
      public Vector3 Color;
      public float Size;
  }
  ```

### 2.4 Consumer Usage in Wayward Beyond

`WaywardBeyond.Client` consumes these components:
- Voxel terrain chunks use `VoxelEntityBuilder.cs:107-114` to produce opaque and transparent meshes.
- Billboard sprites use `BillboardSystem.cs:73-86` for item drops and mobs.
- First-person tools and weapons use `PlayerViewModelSystem.cs:82-83`.
- Voxel light emitters generate `LightComponent` via `LightEntityDecorator.cs:19`.
- Settings are displayed in `SettingsPage.cs:261-299` and persisted in `ConfigManager.cs:49-65`.

## 3. Goals and Non-goals

### Goals:
- Implement dynamic directional shadows using Cascaded Shadow Maps (CSM).
- Implement dynamic local shadows for a strictly budgeted number of active point lights.
- Provide both sharp shadows (single hardware comparison tap) and soft shadows (Percentage-Closer Filtering).
- Support master enable/disable and quality controls via `RenderSettings` and game UI.
- Support per-light shadow casting toggle on `LightComponent` and directional light.
- Support per-mesh shadow casting and shadow receiving toggles on `RenderOptions`.
- Maintain zero heap allocations on render and tick paths.
- Maximize GPU throughput with hardware depth-only rasterization (`glDrawBuffer(GL_NONE)`).
- Provide clean API integration for `WaywardBeyond.Client`.

### Non-goals:
- Screen-Space Contact Shadows (SSCS) or ray-traced shadows.
- Percentage-Closer Soft Shadows (PCSS) with dynamic penumbra search kernels.
- Unbounded shadow-casting point lights (every point light rendering 6 cubemap faces is prohibitive).
- Transparent shadow casting (alpha-colored shadows).
- Shadow support for legacy forward or deferred pipelines.

## 4. Technical Design

```
+---------------------------------------------------------------------------------+
|                               Render Thread Frame                               |
|                                                                                 |
|  1. PreRender:                                                                  |
|     +-----------------------------------------------------------------------+   |
|     | Directional Light Pass:                                               |   |
|     | Compute cascade matrices & snap to texels                             |   |
|     | Render opaque casters (CastShadows == true) -> GL_TEXTURE_2D_ARRAY    |   |
|     +-----------------------------------------------------------------------+   |
|     +-----------------------------------------------------------------------+   |
|     | Budgeted Point Light Pass (up to N lights):                           |   |
|     | Select closest casters -> Render to GL_TEXTURE_CUBE_MAP_ARRAY         |   |
|     +-----------------------------------------------------------------------+   |
|     +-----------------------------------------------------------------------+   |
|     | SAO & Depth Prepass -> Tile Culling Compute (cull_lights.glsl)        |   |
|     +-----------------------------------------------------------------------+   |
|                                                                                 |
|  2. Main Geometry Pass (GLInstancedRenderer):                                   |
|     +-----------------------------------------------------------------------+   |
|     | Bind CSM Texture Array (Unit 8) & Point Shadow Cubemap Array (Unit 9) |   |
|     | Set uReceiveShadows per instance/target                               |   |
|     | Evaluate pbr_plus.glsl: directional shadow + per-tile point shadows   |   |
|     +-----------------------------------------------------------------------+   |
|                                                                                 |
|  3. PostRender:                                                                 |
|     +-----------------------------------------------------------------------+   |
|     | Blit color -> Bloom blur & blend -> Present                           |   |
|     +-----------------------------------------------------------------------+   |
+---------------------------------------------------------------------------------+
```

### 4.1 Directional Shadows (Cascaded Shadow Maps)

Directional shadows use Cascaded Shadow Maps to cover both immediate player surroundings and distant voxel terrain.

#### Frustum Splitting
The camera view frustum divides into $N$ splits ($1 \le N \le 4$, default 3). The split distances follow a practical split scheme balancing logarithmic and uniform spacing:

$$C_i = \lambda \cdot C_{i,\text{log}} + (1 - \lambda) \cdot C_{i,\text{uniform}}$$

Where:
- $C_{i,\text{log}} = n \cdot \left(\frac{f}{n}\right)^{\frac{i}{N}}$
- $C_{i,\text{uniform}} = n + (f - n) \cdot \frac{i}{N}$
- $\lambda = 0.8$ (tunable balance factor).

#### Texel Stabilization
To eliminate edge crawling and shimmering during camera translation:
1. Calculate the cascade bounding sphere and derive an axis-aligned bounding box.
2. Form an orthographic projection of size $(W, H)$.
3. Compute the texel size in world units: $\Delta x = \frac{W}{\text{Resolution}}$, $\Delta y = \frac{H}{\text{Resolution}}$.
4. Round the light view-matrix translation coordinates to integer multiples of $(\Delta x, \Delta y)$:
   $$P_{\text{snapped}} = \text{floor}\left(\frac{P}{\Delta}\right) \cdot \Delta$$

#### Texture Storage
Cascades share a single 2D Texture Array:
- Target: `GL_TEXTURE_2D_ARRAY`.
- Format: `TextureFormat.Depth16` or `TextureFormat.Depth24f`.
- Dimensions: `(Resolution, Resolution, CascadeCount)`.
- Sampler Parameters: `GL_TEXTURE_COMPARE_MODE = GL_COMPARE_REF_TO_TEXTURE`, `GL_TEXTURE_COMPARE_FUNC = GL_LEQUAL`.
- Filtering: `GL_LINEAR` for soft PCF, `GL_NEAREST` for sharp shadows.

#### Fast Depth-Only Rasterization
During shadow map generation:
- Framebuffer draws with `glDrawBuffer(GL_NONE)` and `glReadBuffer(GL_NONE)`.
- Shaders execute a minimal vertex program (`shadow_depth.glsl`) with no fragment output.
- Hardware activates double-speed Z-only rasterization.

### 4.2 Budgeted Local Point Light Shadows

Point lights cast omnidirectional shadows. Rendering 6 cubemap faces for every light in forward+ causes catastrophic draw call amplification. The system enforces a hard budget:
- `MaxPointShadows`: maximum active point shadow casters ($0 \le N \le 4$, default 2).
- Candidate selection: systems collect all `LightComponent` instances with `CastShadows == true`.
- The render context sorts candidates by priority:
  $$\text{Priority} = \frac{\text{Radius}^2}{\text{DistanceSquared}(\text{CameraPos}, \text{LightPos})}$$
- Top $N$ lights receive a slice index $0 \dots N-1$.
- Storage: `GL_TEXTURE_CUBE_MAP_ARRAY` of dimensions `(PointResolution, PointResolution, 6 * MaxPointShadows)`.
- When `MaxPointShadows == 0`, point shadow resources remain unallocated.

### 4.3 Filtering and Shadow Sampling

#### Bias Strategy
Shadow acne is mitigated using normal-offset bias combined with slope-scaled depth bias:
1. Fragment world position is offset along the geometric surface normal:
   $$P_{\text{biased}} = P + N \cdot (\text{NormalBias} \cdot \text{TexelWorldSize})$$
2. Slope depth bias is applied via `glPolygonOffset(1.1f, 4.0f)` during shadow rasterization.
3. This eliminates self-shadow moire without introducing noticeable light leaks (peter-panning).

#### Sharp vs Soft Filtering
The shader supports two modes via `ShadowFilterMode`:

1. **Sharp Shadows (`ShadowFilterMode.Sharp`)**:
   - Single comparison tap using hardware shadow sampler.
   - Lowest GPU cycle cost.
   - Crisp, stylized edges matching blocky or retro aesthetics.

2. **Soft Shadows (`ShadowFilterMode.Soft`)**:
   - Hardware 2x2 bilinear percentage-closer filtering.
   - Sampling `sampler2DArrayShadow` with `GL_LINEAR` computes four depth tests and interpolates results in hardware.
   - High-quality preset augments this with a 4-tap Poisson disk kernel (4 hardware samples = 16 depth comparisons).
   - Smooth penumbra with minimal fragment shader overhead.

### 4.4 Shading Integration (`pbr_plus.glsl`)

`Swordfish/assets/shaders/pbr_plus.glsl` receives shadow data:

```glsl
#ifdef FRAGMENT
uniform int uShadowsEnabled;
uniform int uShadowFilterMode; // 0 = Sharp, 1 = Soft
uniform float uReceiveShadows;  // 1.0 = receive, 0.0 = skip

// Directional cascade data
layout(binding = 8) uniform sampler2DArrayShadow uDirectionalShadowMap;
uniform mat4 uCascadeViewProj[4];
uniform vec4 uCascadeSplits;
uniform int uCascadeCount;
uniform float uNormalBias;

// Point shadow data
layout(binding = 9) uniform samplerCubeArrayShadow uPointShadowMap;

float SampleDirectionalShadow(vec3 worldPos, vec3 normal, float viewDepth)
{
    if (uShadowsEnabled == 0 || uReceiveShadows < 0.5)
    {
        return 1.0;
    }

    // Identify cascade slice
    int cascadeIndex = uCascadeCount - 1;
    for (int i = 0; i < uCascadeCount - 1; ++i)
    {
        if (viewDepth < uCascadeSplits[i])
        {
            cascadeIndex = i;
            break;
        }
    }

    vec3 biasedPos = worldPos + normal * uNormalBias;
    vec4 clipPos = uCascadeViewProj[cascadeIndex] * vec4(biasedPos, 1.0);
    vec3 projCoords = (clipPos.xyz / clipPos.w) * 0.5 + 0.5;

    if (projCoords.z > 1.0)
    {
        return 1.0;
    }

    if (uShadowFilterMode == 0)
    {
        // Sharp: single tap
        return texture(uDirectionalShadowMap, vec4(projCoords.xy, float(cascadeIndex), projCoords.z));
    }
    else
    {
        // Soft: 4-tap Poisson disk PCF
        float shadow = 0.0;
        const vec2 poissonDisk[4] = vec2[](
            vec2(-0.38,  0.35),
            vec2( 0.45,  0.22),
            vec2(-0.25, -0.42),
            vec2( 0.32, -0.38)
        );
        vec2 texelSize = 1.0 / vec2(textureSize(uDirectionalShadowMap, 0).xy);
        for (int i = 0; i < 4; ++i)
        {
            vec2 offset = poissonDisk[i] * texelSize * 1.5;
            shadow += texture(uDirectionalShadowMap, vec4(projCoords.xy + offset, float(cascadeIndex), projCoords.z));
        }
        return shadow * 0.25;
    }
}
#endif
```

The directional radiance multiplies by `SampleDirectionalShadow(...)`. Ambient light and emissive terms remain unscaled, preventing total blackness in shadowed zones.

## 5. Granular Control Matrix

The design satisfies all user requirements across three control levels:

| Scope | Mechanism | API / Data Structure | Effect When Disabled |
|---|---|---|---|
| **Engine / Global** | `RenderSettings.Shadows.Enabled` | `DataBinding<bool>` | Completely disables shadow render passes and sampler bindings. |
| **Engine Quality** | `RenderSettings.Shadows.Quality` | `DataBinding<ShadowQuality>` | Configures resolution, cascade count, and distance presets. |
| **Engine Filter** | `RenderSettings.Shadows.FilterMode` | `DataBinding<ShadowFilterMode>` | Switches between 1-tap Sharp and PCF Soft filtering. |
| **Per Light** | Directional Light | `DirectionalLightComponent.CastShadows` | Disables directional cascade passes; scene lit with full directional irradiance. |
| **Per Light** | Point / Local Light | `LightComponent.CastShadows` | Light excluded from point shadow map pool; produces standard unshadowed illumination. |
| **Per Mesh / Entity** | Casting | `RenderOptions.CastShadows` | Mesh excluded from shadow depth draw calls (`GLInstancedRenderer`). |
| **Per Mesh / Entity** | Receiving | `RenderOptions.ReceiveShadows` | Mesh fragment shader sets `uReceiveShadows = 0.0`; ignores shadow attenuation. |

### 5.1 Settings Data Structures

Located in `Swordfish/Settings/`:

```csharp
namespace Swordfish.Settings;

public enum ShadowQuality
{
    Off,
    Low,
    Medium,
    High,
    Ultra,
}

public enum ShadowFilterMode
{
    Sharp,
    Soft,
}

public sealed record ShadowSettings
{
    public DataBinding<bool> Enabled { get; private set; } = new(true);
    public DataBinding<ShadowQuality> Quality { get; private set; } = new(ShadowQuality.Medium);
    public DataBinding<ShadowFilterMode> FilterMode { get; private set; } = new(ShadowFilterMode.Soft);
    public DataBinding<int> Resolution { get; private set; } = new(1024);
    public DataBinding<float> MaxDistance { get; private set; } = new(128f);
    public DataBinding<int> CascadeCount { get; private set; } = new(3);
    public DataBinding<int> MaxPointShadows { get; private set; } = new(2);
    public DataBinding<float> NormalBias { get; private set; } = new(0.02f);
    public DataBinding<float> DepthBias { get; private set; } = new(0.005f);
}
```

Integrated into `RenderSettings` (`Swordfish/Settings/RenderSettings.cs`):
```csharp
public ShadowSettings Shadows { get; private set; } = new();
```

### 5.2 Quality Presets

The `Quality` enum provides one-touch configuration:

| Preset | Directional Resolution | Cascade Count | Max Distance | Point Shadows | Point Resolution | Filter Mode |
|---|---|---|---|---|---|---|
| **Off** | 0 | 0 | 0 m | 0 | 0 | Sharp |
| **Low** | 512 | 1 | 48 m | 0 | 0 | Sharp |
| **Medium** | 1024 | 3 | 96 m | 2 | 512 | Soft |
| **High** | 2048 | 3 | 160 m | 4 | 512 | Soft |
| **Ultra** | 2048 | 4 | 256 m | 4 | 1024 | Soft |

## 6. Wayward Beyond API Consumer Integration

`WaywardBeyond.Client` interfaces with dynamic shadows across four domains:

### 6.1 Settings Menu (`SettingsPage.cs`)

`WaywardBeyond.Client/UI/Layers/Menus/SettingsPage.cs` adds display widgets under the display section:

```csharp
// Dynamic Shadows master checkbox
bool shadowsEnabled = ui.Checkbox(
    id: "Checkbox_Shadows",
    text: _localization.GetString("ui.setting.shadows")!,
    isChecked: _renderSettings.Shadows.Enabled,
    _soundEffectService
);
_renderSettings.Shadows.Enabled.Set(shadowsEnabled);

// Soft Shadows checkbox
bool softShadows = ui.Checkbox(
    id: "Checkbox_SoftShadows",
    text: _localization.GetString("ui.setting.soft_shadows")!,
    isChecked: _renderSettings.Shadows.FilterMode == ShadowFilterMode.Soft,
    _soundEffectService
);
_renderSettings.Shadows.FilterMode.Set(softShadows ? ShadowFilterMode.Soft : ShadowFilterMode.Sharp);

// Shadow Quality control (Off, Low, Medium, High, Ultra)
ui.NumberControl(
    id: "Control_ShadowQuality",
    text: _localization.GetString("ui.setting.shadow_quality")!,
    (int)_renderSettings.Shadows.Quality.Get(),
    constraints: new Int2(0, 4),
    display: new Int2(0, 4),
    steps: 4,
    _soundEffectService,
    OnShadowQualityChanged
);
```

Settings automatically persist to `client.toml` through `ConfigManager.ApplySettings()`.

### 6.2 Environment & Directional Sun Setup

Wayward Beyond manages world lighting in `WaywardBeyond.Client/Entry.cs` or an environment system:
- Spawns a directional light entity:
  ```csharp
  Entity sunEntity = store.Alloc(new IdentifierComponent("Sun", null));
  store.AddOrUpdate(sunEntity, new DirectionalLightComponent(
      direction: Vector3.Normalize(new Vector3(-0.6f, -1.0f, -0.4f)),
      color: new Vector3(1.0f, 0.95f, 0.85f),
      intensity: 1.0f,
      castShadows: true
  ));
  ```
- Day/night simulation rotates `DirectionalLightComponent.Direction` over game time.

### 6.3 Voxel Chunk Mesh Configuration

In `WaywardBeyond.Client/Voxels/Building/VoxelEntityBuilder.cs:107-114`:
- **Opaque Meshes**: cast and receive shadows:
  ```csharp
  RenderOptions opaqueOptions = _renderOptions with
  {
      CastShadows = true,
      ReceiveShadows = true,
  };
  var renderer = new MeshRenderer(data.OpaqueMesh, _opaqueMaterial, opaqueOptions);
  ```
- **Transparent Meshes** (water, glass): receive shadows, do not cast:
  ```csharp
  RenderOptions transparentOptions = _transparentRenderOptions with
  {
      CastShadows = false,
      ReceiveShadows = true,
  };
  renderer = new MeshRenderer(data.TransparentMesh, _transparentMaterial, transparentOptions);
  ```

### 6.4 Billboards, Viewmodels, and Local Lights

- **Billboards (`BillboardSystem.cs:73`)**:
  - Item drops and nameplates set `CastShadows = false, ReceiveShadows = false`.
  - Character sprite impostors set `CastShadows = true, ReceiveShadows = true`.
- **First-Person Viewmodel (`PlayerViewModelSystem.cs:82`)**:
  - Held tool or weapon sets `CastShadows = false, ReceiveShadows = true`.
  - Disabling casting prevents weapons from throwing unnatural shadows onto adjacent walls directly before the camera.
- **Voxel Light Decorator (`LightEntityDecorator.cs:19`)**:
  - Standard decorative bricks set `CastShadows = false` to preserve the local shadow budget.
  - Interactive player items (torches, flashlights, searchlights) opt in with `CastShadows = true`.

## 7. Performance & Resource Metrics

### 7.1 Memory Footprint (VRAM)

Shadow maps use dedicated depth buffers allocated during initialization or resize:

| Preset | Directional Memory | Point Shadow Memory | Total VRAM |
|---|---|---|---|
| **Off** | 0 MB | 0 MB | **0.0 MB** |
| **Low** ($1 \times 512^2$, 16-bit) | 0.5 MB | 0.0 MB | **0.5 MB** |
| **Medium** ($3 \times 1024^2$, 16-bit) | 6.0 MB | 3.0 MB ($2 \times 6 \times 512^2$, 16-bit) | **9.0 MB** |
| **High** ($3 \times 2048^2$, 24-bit) | 36.0 MB | 18.0 MB ($4 \times 6 \times 512^2$, 24-bit) | **54.0 MB** |
| **Ultra** ($4 \times 2048^2$, 24-bit) | 48.0 MB | 24.0 MB ($4 \times 6 \times 1024^2$, 24-bit) | **72.0 MB** |

Memory is static. No runtime allocations occur during gameplay.

### 7.2 CPU Performance Metrics

- **Hot-Path Allocations**: 0 bytes per frame.
- **Frustum Culling**:
  - Cascade bounding sphere tests use SIMD `Vector3` operations.
  - Voxel chunk AABBs tested against cascade bounds. Chunks outside cascade bounds skip draw calls.
- **Instancing Buffer Reuse**:
  - Shadow depth passes reuse the existing `BufferObject<Matrix4x4> ModelsBufferObject` bound on `GLRenderTarget`.
  - No matrix transformations recomputed on the CPU for instances.
- **Dirty Checking**:
  - Static directional light angle and stationary camera skip distant cascade re-renders if voxel chunks report no mesh edits.

### 7.3 GPU Performance Metrics

- **Double-Speed Z Rasterization**: `glDrawBuffer(GL_NONE)` bypasses pixel shader hardware for depth-only rendering.
- **Tile Compute Synergy**: Light culling (`cull_lights.glsl`) remains unchanged; shadow slice indices are passed directly through the SSBO.
- **Filtered Tap Efficiency**: Hardware `GL_LINEAR` on shadow samplers evaluates 4 depth taps per instruction.
- **Target Frame Budget**:
  - Medium Preset: $< 1.2\text{ ms}$ GPU shadow generation time on baseline GTX 1060 / Radeon RX 580 class hardware at 1080p.

## 8. Development & Implementation Plan

### Phase 1: Engine Core & Data Model
- Add `ShadowQuality`, `ShadowFilterMode`, and `ShadowSettings` in `Swordfish/Settings/`.
- Add `ShadowSettings` property to `RenderSettings`.
- Add `CastShadows` and `ReceiveShadows` booleans to `RenderOptions`.
- Add `CastShadows` boolean to `LightComponent`.
- Create `DirectionalLightComponent` in `Swordfish/ECS/`.
- Update `GPULight` struct packing with shadow parameters (48 bytes, std430 compliant).
- Commit boundary: standalone engine commit `[E] Add shadow configuration and component types`.

### Phase 2: OpenGL Shadow Resources & Depth Pass
- Implement `DirectionalShadowMap` using `GL_TEXTURE_2D_ARRAY` and FBO layer binding.
- Create `shadow_depth.glsl` (depth-only vertex shader).
- Implement cascade frustum calculation and texel stabilization math.
- Add shadow pass dispatch inside `ForwardPlusRenderingPipeline.PreRender`.
- Update `GLInstancedRenderer` to honor `RenderOptions.CastShadows`.
- Commit boundary: standalone engine commit `[E] Add cascaded shadow map renderer`.

### Phase 3: Forward+ Shader Integration & Filtering
- Update `Swordfish/assets/shaders/pbr_plus.glsl`:
  - Declare shadow sampler uniforms (`uDirectionalShadowMap`, `uPointShadowMap`).
  - Implement `SampleDirectionalShadow` with sharp and soft PCF branches.
  - Apply normal-offset bias and depth bias.
  - Integrate shadow factor into `shade()` directional lighting evaluation.
- Update `ShaderActivationCallback` in `ForwardPlusRenderingPipeline` to bind shadow textures and uniforms.
- Commit boundary: standalone engine commit `[E] Integrate dynamic shadows into forward+ shader`.

### Phase 4: Budgeted Point Light Shadows
- Implement `PointShadowMapArray` using `GL_TEXTURE_CUBE_MAP_ARRAY`.
- Implement distance/screen-size priority sorter for shadow-casting point lights.
- Link point shadow indices to `GPULight` SSBO buffer.
- Implement point shadow sampling in `pbr_plus.glsl`.
- Commit boundary: standalone engine commit `[E] Add budgeted point light shadow maps`.

### Phase 5: Wayward Beyond Consumer Integration
- Update `WaywardBeyond.Client/UI/Layers/Menus/SettingsPage.cs` with shadow controls.
- Update `VoxelEntityBuilder.cs` to set `CastShadows` / `ReceiveShadows` on opaque and transparent chunks.
- Update `BillboardSystem.cs` and `PlayerViewModelSystem.cs` with appropriate render options.
- Spawn and update world sun entity with `DirectionalLightComponent`.
- Commit boundary: game commit `Hook up dynamic shadows in client and settings`.

### Phase 6: Quality Gates & Documentation Pass
- Run `dotnet build`.
- Run engine test suite: `dotnet test Swordfish.Tests`, `dotnet test Reef.Tests`, `dotnet test Reef.Text.Tests`, `dotnet test WaywardBeyond.Voxels.Tests`.
- Add unit tests for cascade split calculation, texel snapping, and bias functions in `Swordfish.Tests`.
- Update `docs/specs/config-schemas.md` with `ShadowSettings` table.
- Create `docs/specs/rendering-shadows.md` recording the source-of-truth `file:line` pointers.
- Update `docs/README.md` subject index.
- Commit boundary: docs pass commit `Update docs for dynamic shadows`.

## 9. Verification & Quality Assurance

1. **Precision & Shimmering**:
   - Verify that stationary camera observing a stationary object exhibits zero shadow edge crawl when looking around.
   - Verify that shadow acne does not appear on flat voxel surfaces aligned with coordinate axes.
2. **Exclusion Verification**:
   - Verify that setting `CastShadows = false` on viewmodel prevents the held pickaxe/weapon from casting shadows.
   - Verify that transparent glass and water blocks do not cast dark opaque shadows.
   - Verify that setting `ReceiveShadows = false` on billboards keeps nametags uniformly bright.
3. **Allocation Gate**:
   - Measure per-frame allocations during active player movement; confirm zero heap allocation on the render thread.
4. **Hardware Compatibility**:
   - Verify fallback behavior when MSAA or SAO is toggled concurrently with dynamic shadows.
   - Confirm proper execution on Intel, AMD, and NVIDIA OpenGL drivers.

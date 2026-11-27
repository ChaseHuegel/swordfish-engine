# Particle System

One subject: CPU-simulated, GPU-instanced world-space particles in the `Swordfish` engine.

## Asset format (`assets/particles/**/*.toml`)

Schema: `Swordfish/IO/ParticleEffectDefinition.cs`. Runtime asset:
`Swordfish/Graphics/Particles/ParticleEffect.cs`. Loader:
`Swordfish/IO/ParticleEffectDatabase.cs`. Asset IDs are relative to `particles/`
without the `.toml` extension.

| Key | Type | Default | Notes |
|---|---|---|---|
| `Texture` | string? | null | optional texture relative to `textures/`, or null for solid square quads |
| `BlendMode` | enum | `Alpha` | `Alpha` or `Additive` (`ParticleBlendMode`) |
| `Rate` | float | `0.0` | continuous particles per second for entity emitters, `>= 0` |
| `BurstCount` | int | `1` | default particle count per burst, `>= 0` |
| `MaxParticles` | int | `128` | per-emitter or per-effect burst live cap, `>= 1` |
| `LifetimeMin` / `LifetimeMax` | float | `0.5` | particle lifetime in seconds, `> 0` |
| `SpeedMin` / `SpeedMax` | float | `1.0` | initial speed in world units per second, `>= 0` |
| `SpreadDegrees` | float | `15.0` | cone half-angle in degrees around direction, `0..180` |
| `SpawnRadius` | float | `0.0` | spherical spawn radius jitter in world units, `>= 0` |
| `StartSizeMin` / `StartSizeMax` | float | `0.1` | initial quad size in world units, `> 0` |
| `EndSizeMin` / `EndSizeMax` | float | `0.1` | final quad size in world units, `>= 0` |
| `StartColor` | string | `"#ffffff"` | primary `#RRGGBB` or `#RRGGBBAA` sRGB hex decoded to linear at load |
| `SecondaryStartColor` | string? | null | optional second start hex color mixed randomly per spawned particle |
| `EndColor` | string | `"#ffffff"` | final `#RRGGBB` or `#RRGGBBAA` sRGB hex decoded to linear at load |
| `StartAlpha` / `EndAlpha` | float | `1.0` / `0.0` | linear alpha fallback for 6-digit hex colors, `0..1` |
| `EmissiveIntensity` | float | `0.0` | HDR emissive boost written to `FragColor` and `BrightColor`, `>= 0` |
| `Drag` | float | `0.0` | exponential velocity damping per second, `>= 0` |
| `Gravity` | `{ X, Y, Z }` | `0, 0, 0` | world acceleration per second squared |
| `InitialRotationMin` / `InitialRotationMax` | float | `0.0` | initial billboard angle in degrees |
| `AngularVelocityMin` / `AngularVelocityMax` | float | `0.0` | billboard spin in degrees per second |

Invalid definitions log an error at load and are skipped.

## Simulation and API

- **ECS emitters:** Attach `ParticleEmitterComponent` alongside `TransformComponent`.
  `ParticleSystem` emits continuously while `Playing` is true, rotating `Offset`
  and `LocalDirection` by the entity orientation. When `Effect` is null,
  `ParticleSystem.ResolveEmitterEffect` (`Swordfish/Graphics/Particles/ParticleSystem.cs:445-482`)
  resolves `EffectId` once through `IAssetDatabase<ParticleEffect>`, caches the
  loaded asset, and logs unknown ids once without throwing.
- **Emitter muting:** Attach `ParticleEmitterMutedComponent`
  (`Swordfish/Graphics/Particles/ParticleEmitterMutedComponent.cs`) alongside
  `ParticleEmitterComponent` to suppress new particle spawns and reset the
  emission accumulator without mutating `Playing`. Live particles expire
  normally, and emission resumes when the component is removed.
- **Color tinting:** Both `ParticleEmitterComponent` and `ParticleBurstRequest`
  carry an optional packed `0xRRGGBBAA` sRGB `TintRgba` (`0` leaves the effect
  untinted). `ParticleEffect.PackTint` (`Swordfish/Graphics/Particles/ParticleEffect.cs:165-172`)
  packs normalized sRGB color and linear alpha into `uint`. `ParticleEffect.UnpackTintLinear`
  (`Swordfish/Graphics/Particles/ParticleEffect.cs:175-192`) converts `TintRgba`
  into linear color and multiplies `StartColor` and `EndColor` at spawn time.
- **One-shot bursts:** Inject `IParticleService` and call `EmitBurst`. The
  7-parameter `EmitBurst` overload (`Swordfish/Graphics/Particles/IParticleService.cs:19-27`,
  `ParticleSystem.cs:98-140`) validates a finite position and direction,
  finite non-negative `speedScale`, finite positive `sizeScale`, and positive
  optional `count`, returning failure on invalid inputs before resolving
  `effectId` and forwarding `count`, `speedScale`, `sizeScale`, and `tintRgba`.
  Requests queue through a `ConcurrentQueue` stamped with a monotonic clear
  generation and drain on the ECS thread. `EmitBurst` drops the request while
  `RenderSettings.Particles.Enabled` is false or when the queue holds
  `MAX_PENDING_BURSTS` (`256`) requests. `Clear()` increments the clear
  generation so pre-clear bursts drop while subsequent bursts spawn after the
  reset.
- **Emitter safety and culling:** Non-finite emitter transforms and directions are skipped before allocating state or corrupting batch bounds. Batch extents include diagonal billboard rotation (`0.71` times particle size).
- **Runtime settings:** `RenderSettings.Particles.Enabled`,
  `RenderSettings.Particles.MaxParticles`, `RenderSettings.Particles.Density`,
  and `RenderSettings.Particles.BatchCellSize` (see [config-schemas](config-schemas.md)).
  Disabling particles clears all live particles and queued bursts, and further
  `EmitBurst` calls drop without queueing. `Density`
  defaults to `1.0`, clamps to `0..float.MaxValue`, and scales emitter rates and
  burst counts (`0` stops new spawns while live particles expire normally).
  `BatchCellSize` defaults to `8.0` world units, clamps positive values to at
  least `1.0`, and falls back to `8.0` when non-positive or non-finite.
- **Debug overlay:** `PerformanceStatsOverlay`
  (`WaywardBeyond.Client/UI/PerformanceStatsOverlay.cs:57-60`) displays
  `Particles: <active> / <max>` (or `Particles: disabled`) in the F3 debug
  overlay using `IParticleService.ActiveParticleCount` and
  `RenderSettings.Particles`.

## Threading and rendering

`ParticleSystem` runs on the ECS thread with flat pooled arrays and zero per-tick
allocations. `ParticleSystem.PackSnapshotBatches`
(`Swordfish/Graphics/Particles/ParticleSystem.cs:606-697`) partitions live
particles into batches keyed by `(Texture?, BlendMode, EmitterOwner, spatial cell)`
using `RenderSettings.Particles.BatchCellSize`. Each batch computes tight
world-space bounds for its localized particles so off-screen emitters and cells
cull independently and transparent depth sorting matches nearby transparent
meshes. `ParticleSystem.PublishSnapshot`
(`Swordfish/Graphics/Particles/ParticleSystem.cs:589-604`) publishes batches
through a triple-buffered `ParticleRenderSnapshot`. `ParticleRenderSnapshot.Reset`
clears batch references so unused textures release cleanly.

`GLParticleRenderer` implements `IWorldSpaceRenderStage` and acquires the newest
snapshot once per frame in `PreRender`. All particle batches submit during the shared
transparent pass in `RenderPipeline`, sorted back-to-front per batch. `Alpha` batches
also sort instances back-to-front on the render thread before streaming to the GPU.
Disposed batch textures are skipped via `GLRenderContext.TryBindTexture`, and additive
blending restores the default `(SrcAlpha, OneMinusSrcAlpha)` blend function after drawing.

`Swordfish/assets/shaders/particles.glsl` expands each `ParticleInstance` into a
camera-facing quad in view space, preserves linear HDR values, and outputs
`BrightColor` for Forward+ bloom. See [rendering-color](rendering-color.md).

## Source of truth

- Particle types: `Swordfish/Graphics/Particles/{ParticleBlendMode,ParticleRange,ParticleEffect,ParticleEmitterComponent,ParticleEmitterMutedComponent,ParticleBurstRequest,IParticleService,ParticleInstance,ParticleRenderBatch,ParticleRenderSnapshot,ParticleSystem}.cs`
- Asset loading: `Swordfish/IO/{AssetPaths,ParticleEffectDefinition,ParticleEffectDatabase}.cs`
- Renderer, texture guard, and shader: `Swordfish/Graphics/SilkNET/OpenGL/Renderers/GLParticleRenderer.cs`, `Swordfish/Graphics/SilkNET/OpenGL/GLRenderContext.cs:71-79,214-218`, `Swordfish/assets/shaders/particles.glsl`
- Pipeline time uniform: [rendering-time](rendering-time.md)
- Physics ignore-entity raycast: `Swordfish/Physics/IPhysics.cs:22`, `Swordfish/Physics/Jolt/{JoltPhysicsSystem.cs:123-140,JoltRaycastRequest.cs:1-80}`
- Settings and DI wiring: `Swordfish/Settings/{ParticleSettings,RenderSettings}.cs`, `Swordfish/EngineContainer.cs:87-89,109-111,133,140`

## Tests that pin this

- `Swordfish.Tests/ParticleSystemTests.cs`
- `Swordfish.Tests/RaycastPhysicsTests.cs`
- `Swordfish.Tests/ConfigSerializationTests.cs`
- `WaywardBeyond.Tests/PerformanceStatsOverlayTests.cs`

# Mining Laser Effects

One subject: client and server presentation for the mining laser beam, impact light, and particle sparks. The underlying engine particle system and asset format live in [particles](particles.md). Interaction timing and break cadence live in [interaction-pipeline](interaction-pipeline.md).

## Beam activation and replication

Players fire the mining laser continuously by holding the primary button while an item with `ToolType.Mining` is active.

- **Local prediction:** `MiningLaserEffectSystem` (`WaywardBeyond.Client/Systems/MiningLaserEffectSystem.cs:26-169`) runs only while `WaywardBeyond.IsInGame()`. It queries local players only while `WaywardBeyond.IsPlaying()`, then checks unblocked interaction state, left mouse hold, and `ToolType.Mining`. It traces along the camera look ray via `AimRay.TryTrace` (`WaywardBeyond.Gameplay/Interactions/AimRay.cs:29-61`) using `IPhysics.Raycast(in Ray, int ignoreEntity)` to skip the shooter collider up to `CharacterDefaults.DEFAULT_REACH` (`9.5`).
- **Server authority:** `ServerLaserBeamSystem` (`WaywardBeyond.Server/Systems/ServerLaserBeamSystem.cs:19-169`) queries player mirrors in `ServerComposition` after `ServerInteractionSystem` and before `NetworkPublishSystem` (`WaywardBeyond.Server/ServerComposition.cs:106-111`). `ServerPhysicsSystem.Raycast(in Ray, int ignoreEntity)` (`WaywardBeyond.Server/Systems/ServerPhysicsSystem.cs:46-49`) forwards the ignore-entity filter into Jolt. The system traces `AimRay.TryTrace`, writes `LaserBeamComponent` (`WaywardBeyond.Data/Components/LaserBeamComponent.cs:7-14`), and change-gates updates against square distance thresholds (`0.0004` for `Target` and `0.001` for `Normal`) to avoid empty snapshot traffic. `LaserBeamComponent` registers `ReplicationAudience.OthersOnly` (`WaywardBeyond.Networking/NetworkingComposition.cs:34-39`) so the server omits self-echo snapshots and removals for the shooter. Releasing fire or switching tools removes `LaserBeamComponent`, replicating a removal to remote observers.
- **Remote presentation:** `MiningLaserEffectSystem` (`WaywardBeyond.Client/Systems/MiningLaserEffectSystem.cs:171-184`) renders replicated `LaserBeamComponent` entities that carry `BodyViewComponent` and defensively skips entities carrying `PlayerComponent`. Non-finite coordinates are dropped. Remote beams keep updating while `GameState.Paused` because the pause scrim leaves the world visible, matching `ClientParticleReplicationSystem` (`WaywardBeyond.Client/Systems/ClientParticleReplicationSystem.cs:43`). A paused beam that no longer re-appears is released by the normal stale-owner sweep.

## Beam mesh, orientation, and shader

`LaserBeamMesh` (`WaywardBeyond.Client/Graphics/LaserBeamMesh.cs:11-140`) builds a shared unit cylinder (`Shared = true`) along `-Z` from `0` to `-1` with 8 radial segments and explicit vertex bounds for frustum culling. It emits the outer sheath (`0.048` to `0.056` radius, `Uv.Z = 1`) before the inner core (`0.015` to `0.018` radius, `Uv.Z = 0`) with outward counter-clockwise triangle winding so transparent blending draws the core inside the sheath in one draw call.

- **Muzzle and close-range origin:** `LaserBeamGeometry.ComputeFirstPersonMuzzle` (`WaywardBeyond.Client/Systems/LaserBeamGeometry.cs:22-45`) transforms the barrel nozzle `(0, 0, -1.125)` through the active `ViewModelComponent` child transform (`ChildComponent`) matching `ChildSystem` (`Swordfish/ECS/ChildSystem.cs:23-25`). When no rendered `ViewModelComponent` child is present, `MiningLaserEffectSystem` (`WaywardBeyond.Client/Systems/MiningLaserEffectSystem.cs:93-102`, `:162-165`) falls back to the item's static `ModelDefinition`. While the local player holds primary fire with a `ToolType.Mining` item, `PlayerViewModelSystem` (`WaywardBeyond.Client/Systems/PlayerViewModelSystem.cs:43-53`, `:94-112`, `:260-299`) applies a slight periodic shake to the view-model `ChildComponent` around its resting pose through its shared `ViewModelAnimationProfile` path. This keeps the beam origin anchored to the shaken barrel nozzle. `ToolAimGeometry.ComputeSafeStart` (`WaywardBeyond.Gameplay/Interactions/ToolAimGeometry.cs:27-42`) steps backward to chest fallbacks when the hit sits behind the barrel muzzle. `ToolAimGeometry.ComputeRemoteOrigin` (`ToolAimGeometry.cs:18-21`) anchors remote beams near the third-person body offset.
- **Orientation:** `LaserBeamGeometry.CreateBeamOrientation` (`LaserBeamGeometry.cs:48-68`) aligns local `-Z` to the normalized target vector and scales local `Z` by beam length with a sinusoidal thickness pulse.
- **Material and shader:** `WaywardBeyond.Client/assets/materials/effects/laser_beam.toml` uses shader `laser_beam` (`WaywardBeyond.Client/assets/shaders/laser_beam.glsl:1-55`) with `AlphaMode = "Blend"`. The fragment stage computes Fresnel sheath falloff, animated plasma waves driven by `uTime`, Endesga-32 linear palette colors (`#ffffff`, `#fee761`, `#feae34`, `#f77622`, `#e43b44`), and non-premultiplied HDR `BrightColor` bloom output. `uTime` and the local pulse accumulator wrap at the shared interval in [rendering-time](rendering-time.md).

## Impact light, continuous sparks, and break bursts

When a beam hits geometry (`hit == true`), `MiningLaserEffectSystem` (`WaywardBeyond.Client/Systems/MiningLaserEffectSystem.cs:224-251`) enables a warm flickering `LightComponent` (`1.5` radius, `#feae34` base color) and sets `Playing = true` on a `ParticleEmitterComponent` using `mining_laser_impact` (`WaywardBeyond.Client/assets/particles/mining_laser_impact.toml`), oriented along the surface normal. When aiming into open space (`hit == false`), the system keeps the beam active, removes `LightComponent`, and pauses the emitter.

Brick destruction raises one-shot break spark bursts on both the predicting player and remote observers.

- **Local prediction breaks:** `PlayerInteractionService.FirePresentationHook` (`WaywardBeyond.Client/Systems/PlayerInteractionService.cs:435`, `PlayerInteractionService.cs:718-753`) passes `clickedPoint` and `hitNormal` in `BrickEvent` (`WaywardBeyond.Client/Events/BrickEvent.cs:7-17`).
- **Remote breaks:** `ClientVoxelReconcileSystem` (`WaywardBeyond.Client/Systems/ClientVoxelReconcileSystem.cs:150-172`) resolves the world brick center via `InteractionResolver.BrickToWorldSpace` and emits `BrickEvent` for unpredicted edits.
- **Burst listener and cleanup:** `BrickBreakParticleListener` (`WaywardBeyond.Client/Services/BrickBreakParticleListener.cs:11-28`) listens for `BrickEventKind.Break` with a finite world position and calls `IParticleService.EmitBurst` with `mining_laser_break` (`WaywardBeyond.Client/assets/particles/mining_laser_break.toml`). `ClientCleanupSystem` (`WaywardBeyond.Client/Systems/ClientCleanupSystem.cs:37-45`) calls `IParticleService.Clear()` during world teardown, and `MiningLaserEffectSystem` frees all beam entities whenever the client leaves the in-game states (`MiningLaserEffectSystem.cs:76-80`, `:312-325`).

## Tests that pin this

`WaywardBeyond.Tests/MiningLaserEffectsTests.cs` pins:

- Shipped `mining_laser_impact` and `mining_laser_break` loading with additive blend, HDR emissive intensity, and decoded linear colors.
- `laser_beam.glsl` parser output, non-premultiplied `BrightColor` output, and `effects/laser_beam.toml` material configuration.
- `LaserBeamMesh` shared lifetime flag, non-empty bounds, sheath-before-core draw order, and outward triangle winding.
- `LaserBeamGeometry` muzzle (both static `ModelDefinition` and animated `ChildComponent`) and beam orientation mapping and `ToolAimGeometry` close-range start fallback, remote origin calculation with non-unit parent scale parity with `ChildSystem`, and finite coordinate validation.
- `AimRay.TryTrace` hitting the floor directly underneath a player collider using `IPhysics.Raycast(in Ray, int ignoreEntity)` and rejecting non-finite positions.
- `ServerLaserBeamSystem` tool gating, change-gated dirty state, hit/miss updates, ignore-entity forwarding, shooter self-echo omission across deltas, full syncs, and removals, and loopback `LaserBeamComponent` observer add and removal replication.
- `MiningLaserEffectSystem` local prediction, animated view-model muzzle anchoring, hit/miss light and emitter transitions, remote body replication, duplicate local mirror suppression, non-finite transform and target rejection, paused remote beam visibility with local prediction suppressed, pulse time wrapping, and state exit cleanup.
- `BrickBreakParticleListener`, `ClientVoxelReconcileSystem` remote break world-position events, and `ClientCleanupSystem` particle clearing.

`WaywardBeyond.Tests/PlayerViewModelTests.cs` pins:

- Active primary-fire view-model shake for `ToolType.Mining`, secondary-fire inactivity, exact resting pose restoration on release, blocking UI, pause, and item switch, wrapped animation time with invalid-delta guards, and first-person beam muzzle anchoring to the shaken barrel nozzle.

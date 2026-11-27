# Paint Sprayer Effects

One subject: local predicted and generic server-replicated particle presentation for the paint sprayer (`wb:sprayer`), including nozzle mist streams, surface overspray, and face-paint splatter bursts. The underlying engine particle system and asset format live in [particles](particles.md). Generic emitter replication lives in [networking-particles](networking-particles.md). Per-face painting rules live in [networking-voxel-edits](networking-voxel-edits.md).

## Particle templates and swatch tinting

Six `Alpha`-blended, zero-emissive particle templates ship under `WaywardBeyond.Client/assets/particles/` so every Endesga-32 swatch renders as matte pigment mist instead of additive glow.

- `paint_sprayer_stream.toml` — continuous narrow nozzle cone (`Rate = 75.0`, `SpreadDegrees = 13.0`, light gravity drop) tinted by `TintRgba` while color spraying.
- `paint_sprayer_impact.toml` — continuous wide surface overspray mist (`Rate = 48.0`, `SpreadDegrees = 72.0`) tinted by `TintRgba` along the target surface normal during a hit.
- `paint_sprayer_coat.toml` — one-shot splatter burst (`BurstCount = 18`) tinted by `TintRgba` when a brick face receives or loses color paint.
- `paint_sprayer_solvent_stream.toml`, `paint_sprayer_solvent_impact.toml`, `paint_sprayer_solvent_coat.toml` — untinted pressurized solvent variants (`#ffffff`, `#c0cbdc`, and Endesga-32 cyan `#2ce8f5`) used when secondary fire strips paint.

Both client and server pack active color swatches with `ParticleEffect.PackTint` (`Swordfish/Graphics/Particles/ParticleEffect.cs:165-172`) instead of cloning `ParticleEffect` assets in memory.

## Continuous nozzle stream, replication, and surface mist

`PaintSprayerEffectSystem` (`WaywardBeyond.Client/Systems/PaintSprayerEffectSystem.cs:24-329`) runs on the client ECS thread while `WaywardBeyond.IsPlaying()` is true and drives local first-person predicted emitters (`NozzleEmitterEntity` and `ImpactEmitterEntity`) tagged `"game"`.

- **Input and tool gating:** `UpdateLocalPlayer` (`WaywardBeyond.Client/Systems/PaintSprayerEffectSystem.cs:77-148`) activates when interaction is unblocked, `ToolType.Sprayer` is in the main hand, and left or right mouse is held. Left hold selects `paint_sprayer_stream` and `paint_sprayer_impact` tinted by `InteractionState.SelectedColorPaintId` (falling back to the first registered color paint). Right hold selects the untinted solvent templates.
- **Client input upload:** `ClientInputSystem` (`WaywardBeyond.Client/Systems/ClientInputSystem.cs:93-110,147-158`) stamps `InputComponent.PaintId` from `InteractionState.SelectedColorPaintId` while `PrimaryHeld` is true (or `null` otherwise) and dirties `InputComponent` whenever the swatch changes during active fire.
- **Server authority:** `ServerPaintSprayerSystem` (`WaywardBeyond.Server/Systems/ServerPaintSprayerSystem.cs:25-384`) ticks in `ServerComposition` after `ServerLaserBeamSystem` (`WaywardBeyond.Server/ServerComposition.cs:108`). It resolves the sim-tick `InputComponent` on the player mirror, verifies `ToolType.Sprayer`, validates requested color swatches against `IPaintDatabase`, traces `AimRay.TryTrace`, computes shared aim offsets through `ToolAimGeometry` (`WaywardBeyond.Gameplay/Interactions/ToolAimGeometry.cs:8-71`), and spawns two generic emitters via `IServerParticleService.SpawnEmitter` (`WaywardBeyond.Server/Particles/IServerParticleService.cs:29-42`).
- **Generic emitter layout and gating:** The nozzle emitter attaches to the player entity via `ParticleEmitterBindingComponent.ParentEntity` so `ChildSystem` drives world position on remote clients without server transform updates. Both nozzle and impact emitters set `NetworkComponent.ExcludedRecipientEntity` to the shooter entity via `IServerParticleService.SpawnEmitter` so `NetworkReplicationSystem` omits them from the shooter's snapshots and despawns, avoiding double rendering and wasted wire traffic. Server updates change-gate against squared distance (`0.0004`) and normal (`0.001`) thresholds. Releasing fire, switching tools, non-finite transforms, or player disconnects despawn both emitter entities via `IServerParticleService.DespawnEmitter`.
- **Nozzle origin and safe aim:** `PaintSprayerGeometry.ComputeFirstPersonNozzle` (`WaywardBeyond.Client/Systems/PaintSprayerGeometry.cs:25-48`) computes first-person nozzle world transforms through the active `ViewModelComponent` child transform (`ChildComponent`) or static `[Items.ViewModel]`. `PaintSprayerEffectSystem` (`PaintSprayerEffectSystem.cs:59-72`, `:130-134`) queries `<ViewModelComponent, MeshRendererComponent, ChildComponent>` so the nozzle stream tracks reactive view-model motion and falls back to `[Items.ViewModel]` when unavailable. `ToolAimGeometry` provides safe start fallbacks, remote third-person origins, and aim direction math (`ToolAimGeometry.cs:18-55`).
- **Hit, miss, and teardown:** `AimRay.TryTrace` ignores the shooter collider up to `CharacterDefaults.DEFAULT_REACH` (`9.5`). When `hit == true`, the impact emitter plays at `target + safeNormal * 0.04` along `safeNormal`. On a miss (`hit == false`), the nozzle stream keeps playing while the impact emitter pauses (`Playing = false`) without dirtying impact transform state. Leaving `GameState.Playing` frees all locally predicted entities.

## Local and remote face-paint bursts

Both local prediction and remote authoritative color edits raise `PaintFaceEvent` (`WaywardBeyond.Client/Events/PaintFaceEvent.cs:7-19`) with `PaintFaceAction` (`WaywardBeyond.Client/Events/PaintFaceAction.cs:4-8`).

- **Local prediction:** `PlayerInteractionService.AttemptPaintInteraction` (`WaywardBeyond.Client/Systems/PlayerInteractionService.cs:471-552`) captures `clickedPoint` and `hitNormal`. `FirePaintFaceEvent` (`PlayerInteractionService.cs:650-668`) invokes `PaintFaceEvent` for `PaintKind.Color` predictions (`Clear` passes the first stripped `ColorPaintId` from `originalOverrides`).
- **Remote edits:** `ClientVoxelReconcileSystem.ApplyAuthoritativeFaceEdit` (`WaywardBeyond.Client/Systems/ClientVoxelReconcileSystem.cs:243-327`) suppresses `PaintFaceEvent` when the incoming `VoxelFaceEditMessage` confirms or snaps a matching sequence in `PendingInteractionQueue`. For unpredicted remote `PaintKind.Color` edits, it captures any stripped `ColorPaintId` before mutation, computes the outward world normal and cell offset via `PaintSprayerGeometry.ComputeFaceWorldNormalAndOffset` (`PaintSprayerGeometry.cs:43-80`), and invokes `PaintFaceEvent`.
- **Burst listener:** `PaintFaceParticleListener` (`WaywardBeyond.Client/Services/PaintFaceParticleListener.cs:12-72`) offsets finite surface points by `0.04` along the normal, emits one tinted `paint_sprayer_coat` burst on `Apply`, and emits both an untinted `10`-particle `paint_sprayer_solvent_coat` burst and (when stripped color exists) a tinted `10`-particle `paint_sprayer_coat` burst on `Clear`. The listener ignores `PaintKind.Texture` events.
- **DI wiring:** `EventInvoker<PaintFaceEvent>` and `PaintSprayerEffectSystem` register in `WaywardBeyond.Client/Injector.cs:114,363-364`.

## Tests that pin this

`WaywardBeyond.Tests/PaintSprayerEffectsTests.cs` pins the following contracts.

- Shipped color and solvent sprayer templates loading with `Alpha` blending, zero emissive boost, and solvent cyan end color.
- `PaintSprayerGeometry` static and animated `ChildComponent` first-person nozzle parity with `ChildSystem` including non-unit parent scale and face-mask world normal calculation across rotated voxels and structures, plus `ToolAimGeometry` remote origins, close-range fallbacks, aim directions, and finite checks.
- `PaintSprayerEffectSystem` tool gating, LMB color spraying (`EffectId` and `TintRgba`), live swatch switching, hit and miss transitions, RMB solvent spraying, blocker and non-finite release, button release, and game-state exit cleanup.
- `ServerPaintSprayerSystem` tool gating, color swatch validation against `IPaintDatabase`, change-gated dirty state, zero spurious nozzle transform updates, solvent switching, hit and miss transitions, shooter snapshot omission, remote observer child-tracked rendering, release despawn, and disconnect cleanup.
- `PaintFaceParticleListener` apply, clear, texture, and non-finite handling, `PlayerInteractionService.FirePaintFaceEvent`, and `ClientVoxelReconcileSystem` remote unpredicted color bursts versus predicted echo suppression.

`WaywardBeyond.Tests/ClientInputIdleTests.cs` pins active swatch propagation on `InputComponent.PaintId` during `PrimaryHeld` and clear `PaintId` for solvent or idle input.

`WaywardBeyond.Tests/PlayerViewModelTests.cs` pins first-person nozzle emitter alignment with reactive `PlayerViewModelSystem` motion while moving and turning.

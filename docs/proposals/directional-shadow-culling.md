# Bug Fix — Directional light shadows depend on camera orientation

Status: implemented.

Issue: [ChaseHuegel/swordfish-engine#693](https://github.com/ChaseHuegel/swordfish-engine/issues/693).
Regression commit: `00c9d175` (rendering perf and physics alloc optimizations).

Scope: the engine rendering tier (`Swordfish`). No game-tier change.

## 1. Problem

Directional light shadows appear and disappear when the camera rotates.
Commit `00c9d175` introduced this regression.

## 2. Root cause

`Frustum.FromViewProjection` built the near plane for the Direct3D clip
convention (`Swordfish/Graphics/Frustum.cs:34`):

```csharp
NormalizePlane(m.M13, m.M23, m.M33, m.M43),   // z_clip >= 0
```

That plane keeps `z_clip >= 0`. OpenGL clips every drawn matrix to
`-w_clip <= z_clip <= w_clip`, whatever depth range the projection was built
for. The near plane must keep `z_clip + w_clip >= 0`. The other five planes
used the correct OpenGL form. The near plane dropped the `w_clip` term.

The engine has two shadow projection builders, and both map depth to [-1, 1]:

- `ShadowCascadeMath.CreatePerspectiveOpenGL` (`ShadowCascadeMath.cs:26-38`).
- `ShadowCascadeMath.CreateOrthographicOpenGL` (`ShadowCascadeMath.cs:9-23`).

The effect depends on the depth curve.

- Perspective camera: the depth curve is hyperbolic. The camera projection comes
  from `Matrix4x4.CreatePerspectiveFieldOfView`, which maps depth to [0, 1]. GL
  still clips it at NDC depth -1, so the bad plane culls geometry between half
  the near distance and the near distance. The loss is small and unnoticed.
- Orthographic cascade: the depth curve is linear. The bad plane cuts the depth
  range exactly in half.

The light camera sits two padded radii in front of the cascade centre, and the
depth range spans four padded radii (`ShadowCascadeMath.cs:186-195`). The
cascade centre therefore lands on the cut. Every caster between the light and
the cascade centre fails the near plane test.

Culling makes this visible. Commit `00c9d175` added per-pass frustum culling.
Before that commit, the shadow passes drew every caster.

- `GLInstancedRenderer.RenderShadowPass` culls with
  `Frustum.FromViewProjection(view * projection)`
  (`GLInstancedRenderer.cs:355`).
- `GLMeshBatchRenderer.RenderShadowPass` does the same
  (`GLMeshBatchRenderer.cs:113`).
- Both passes discard the whole target or range when one plane test fails
  (`GLInstancedRenderer.cs:375`, `:408`, `GLMeshBatchRenderer.cs:341-350`).

The rejected half is the world-space half-space
`dot(p - cascadeCentre, lightDirection) < 0`. That half depends on the view
direction, so the surviving shadow region moves with the camera. This matches
the reported symptom.

The docs already state the intended behavior:
"The directional light frustum extends toward the light. A caster between the
light and the cascade remains in the shadow map."
(`docs/specs/rendering-shadows.md:25-26`).

## 3. Evidence

A standalone probe reproduces the engine math with the client settings: FOV
60°, aspect 16:9, near 0.1 m, far 1000 m, `MaxDistance` 96 m, 3 cascades,
resolution 1024, sun direction `(-0.35, -0.85, -0.4)`.

### 3.1 Cascade slice corners

The eight corners of cascade 0, expressed in the cascade clip volume:

| Corner | NDC z | Current near plane | Correct near plane |
|---|---|---|---|
| near 0 | 0.073 | 1.491 | 22.031 |
| near 1 | 0.076 | 1.563 | 22.103 |
| near 2 | 0.077 | 1.589 | 22.129 |
| near 3 | 0.081 | 1.661 | 22.201 |
| far 0 | -0.407 | -8.359 | 12.181 |
| far 1 | -0.128 | -2.626 | 17.915 |
| far 2 | -0.026 | -0.527 | 20.013 |
| far 3 | 0.253 | 5.207 | 25.747 |

Three of the eight slice corners fail the current near plane. The far half of
the cascade has no shadow casters. The corrected plane keeps all eight.

### 3.2 Camera pitch sweep

Ground chunks (8 m footprint, 6 m tall AABB) that survive cascade culling:

| Camera pitch | Cascade 0 | Cascade 1 | Cascade 2 |
|---|---|---|---|
| 0° | 4/4 | 21/24 | 212/312 |
| -15° | 4/4 | 18/24 | 149/312 |
| -35° | 4/4 | 11/24 | 64/312 |
| -60° | 4/4 | 5/23 | 8/312 |

Ground culling collapses as the camera pitches down, and the distant cascades
lose most casters first.

## 4. Change

One line in the engine.

```csharp
// Swordfish/Graphics/Frustum.cs:34
NormalizePlane(m.M14 + m.M13, m.M24 + m.M23, m.M34 + m.M33, m.M44 + m.M43),
```

The summary on `FromViewProjection` now states the clip convention: the input
assumes the OpenGL clip volume, with depth in [-1, 1].

No other change is needed. The split distances, the cascade sphere, the texel
snapping, and the shader depth conversion are already correct.

- The orthographic width is exactly `resolution` texels, so center snapping
  matches the shadow texel grid.
- The shader converts the OpenGL depth range to [0, 1]
  (`Swordfish/assets/shaders/pbr_plus.glsl:156-163`).

## 5. Tests

The change adds four tests to `Swordfish.Tests`:

1. `Frustum_KeepsGeometryJustInsideTheNearPlane`: OpenGL perspective projection
   with near 1 m and far 100 m. A box 1.5 m in front of the camera intersects
   the frustum. A box 0.5 m in front does not.
2. `Frustum_KeepsEveryCascadeSliceCorner`: build a cascade matrix. Assert that
   all eight slice corners intersect the cascade frustum.
3. `Frustum_KeepsCasterBetweenTheLightAndTheCascade`: place a caster half a
   radius toward the light from the cascade centre. Assert that it intersects
   the cascade frustum.
4. `CalculateCascadeMatrix_MapsSliceCornersInsideTheClipVolume`: assert that
   each slice corner has NDC x, y, and z in [-1, 1].

Tests 1 to 3 fail before the change. The suite runs on Linux and needs no
window, GPU, or font assets.

## 6. Validation

1. `dotnet build`.
2. `dotnet test Swordfish.Tests`.
3. The remaining quality gates in `AGENTS.md`.
4. Manual check: run the client with shadows on. Rotate and pitch the camera.
   Shadows stay attached to their casters in all directions and at all
   distances. Confirm no shadow acne or peter-panning appears.

The manual check needs the client because `WaywardBeyond.Client` is the only
place that creates a directional light (`WaywardBeyond.Client/Entry.cs:36-42`).

## 7. Risks and alternatives

- Draw cost: the shadow pass draws the casters that it dropped. This is the
  pre-regression behavior. There is no new overdraw beyond the correct frustum.
- Camera-pass culling also changes. It now matches the GL clip volume instead of
  stopping at the nominal near plane. It keeps slightly more geometry near the
  camera and drops none that GL renders.
- Alternative: disable shadow-pass culling. Rejected. It drops a real
  optimization and hides the math error.
- Alternative: move the light camera closer to the cascade centre. Rejected. It
  narrows the caster depth range and cannot fix the clip-convention mismatch.

## 8. Adjacent problem (reported, not fixed here)

`GLRenderContext.RentSceneBuffer` returns a pooled `EntityModel` array that can
be longer than the current entity count (`GLRenderContext.cs:146-161`).
`Tick` copies only `_renderableEntities.Count` items into it
(`GLRenderContext.cs:139-140`). The array length stays at the pool size, so the
renderer reads stale entries from earlier frames. The stale entries add dead
draw calls and inflate the instance-set bounds that culling uses.

This problem does not cause the shadow regression. It is captured as
[0098](/docs/issues/0098-scene-buffer-stale-entities.md) in the backlog.

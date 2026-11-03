# Bug: The scene buffer keeps stale entity models

- Type: bug
- Status: open
- Workflow: ../specs/issues.md

## Problem

`GLRenderContext.RentSceneBuffer` returns a pooled `EntityModel` array that can
be longer than the current entity count
(`Swordfish/Graphics/SilkNET/OpenGL/GLRenderContext.cs:146-161`).

`GLRenderContext.Tick` copies `_renderableEntities.Count` items into that array
and publishes the whole array as `SceneData.Instances`
(`Swordfish/Graphics/SilkNET/OpenGL/GLRenderContext.cs:139-143`).

The renderer reads `RenderScene.EntityModels.Length` entries
(`Swordfish/Graphics/SilkNET/OpenGL/Renderers/GLInstancedRenderer.cs:66`). The
tail of a reused buffer holds `EntityModel` values from an earlier frame, so the
renderer draws meshes at stale matrices and adds their stale bounds to the
instance sets that culling uses.

The defect entered with commit `00c9d175`. It is not related to the directional
shadow culling fix.

## Acceptance criteria

- [ ] A frame with fewer renderable entities publishes a scene array whose
      length equals the entity count. No stale `EntityModel` reaches a render
      stage.
- [ ] A test drives two consecutive ticks with a shrinking entity count and
      asserts that the published scene length matches the live count.

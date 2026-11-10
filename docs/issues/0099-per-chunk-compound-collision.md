# Improvement: Use one physics body with per-chunk compound children

- Type: improvement
- Status: open
- Workflow: [../specs/issues.md](../specs/issues.md)
- Plan: [../plans/voxel-remesh-rendering.md](../plans/voxel-remesh-rendering.md) (Phase G3)

## Problem

Voxel collision is a single flat compound. `VoxelObjectBuilder.CombineCollisions`
copies every cached chunk surface box into three new arrays on every edit
(`WaywardBeyond.Client/Voxels/Building/VoxelObjectBuilder.cs:86-114`). The
resulting `ColliderComponent` replaces the body's shape, so Jolt converts the
whole compound again (`Swordfish/Physics/Jolt/JoltPhysicsSystem.cs:206-241`).
The cost grows with the total surface, not with the edit.

A voxel object must stay one physics body. Players will build ships from voxels.
If each chunk becomes its own body, chunk bodies collide with each other and
tear the ship apart. The fix is one body whose compound children are per-chunk
compounds.

## Acceptance criteria

- [ ] A voxel object has one physics body.
- [ ] Each chunk owns one child compound shape under that body.
- [ ] An edit rebuilds only the dirty chunk's child shape.
- [ ] Chunks of the same object never collide with each other.
- [ ] The whole object still moves as one body.
- [ ] A large-structure edit no longer copies every chunk shape.
- [ ] The benchmark records the collision cost before and after the change.
- [ ] The server collision path is reviewed for the same shape.

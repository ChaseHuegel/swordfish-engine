# Proposal: Batched chunk rendering

- Status: proposed. Not implemented.
- Owner: opencode (deepseek-v4.1-flash).
- Scope: the engine tier (a batched mesh resource, a renderer component, and a
  multi-draw render stage) plus the client adoption. Game content lands
  separately.
- Related plan: [../plans/voxel-remesh-rendering.md](../plans/voxel-remesh-rendering.md)

## 1. Summary

Chunk remeshing created one mesh, one VAO, one VBO, one EBO, one matrix buffer,
and one ECS entity per chunk. The renderer then draws one chunk per draw call.
Frustum culling removes chunks from the list, but every shadow cascade, every
depth pass, and the color pass still walks the visible set. The same chunk is
drawn about 18 times per frame.

Instanced rendering does not fix this. Instancing merges draws that share one
mesh. Chunk meshes are unique. The current instancing groups by
`GLRenderTarget` equality, which includes the VAO
(`Swordfish/Graphics/SilkNET/OpenGL/GLRenderTarget.cs:53-102`). Each chunk has
its own VAO (`GLRenderContext.cs:199-293`), so each chunk is a group of one.

The proposal replaces the per-chunk mesh with a **per-object batched mesh**.
One vertex buffer and one index buffer hold every chunk of one voxel object.
Each chunk owns a range in those buffers. The renderer culls ranges on the CPU
and draws all visible ranges of a batch with one multi-draw call per material
per pass.

This keeps the rebuild win. A rebuild writes one range. It does not create a GL
object, and it does not touch the other chunks. It also removes the per-chunk
ECS entities and the per-chunk render target bookkeeping.

## 2. Current cost

Measured or derived from the code:

- One chunk creates up to three `MeshRenderer` entities, each with a unique
  `Mesh`, VAO, VBO, EBO, and MBO (`WaywardBeyond.Client/Voxels/Building/VoxelEntityBuilder.cs:240-306`).
- Every edit creates a new set of chunk meshes and GL objects, then defers the
  old set for two to five ticks (`MeshRendererCleanup.cs:40-66`).
- `GLInstancedRenderer.PreRender` runs four times per frame and rebuilds three
  `Dictionary<GLRenderTarget, List<Matrix4x4>>` each time
  (`GLInstancedRenderer.cs:40-108`, `ForwardPlusRenderingPipeline.cs:329, 350, 412`).
  Each lookup hashes the target, which hashes `RenderOptions` by reflection.
- The shadow pass draws the visible set once per cascade and once per cube face
  (`ForwardPlusRenderingPipeline.cs:580-785`). Defaults are 3 cascades and 2
  point shadows.
- `RenderOptions.Equals` boxes once per target per pass
  (`GLInstancedRenderer.cs:243-251`).

The expected draw count is:

```
(cascades + 6 * pointShadowLights + 2 depth passes + 1 color pass) * chunks
```

With the defaults that is 18 draws per chunk per frame. With 1,000 visible
chunks it is 18,000 draws.

## 3. How other engines do this

The common pattern is a renderer-owned batch. The game supplies geometry and
bounds. The renderer owns buffers, culling, and draw order.

| Engine | Public surface | Batching location | Lesson |
|---|---|---|---|
| Unity | `MeshRenderer`, `Graphics.DrawMesh*` | SRP Batcher and static/dynamic batching inside the render pipeline | Batching is internal. The public API stays per-object. |
| Godot | `MeshInstance3D`, `ArrayMesh` surfaces, `MultiMesh` | Renderer sorts and batches by pipeline and material | Surfaces are ranges. Instancing is a separate feature. |
| Unreal | `UStaticMeshComponent`, mesh sections, `UInstancedStaticMeshComponent` | Mesh drawing policies batch by vertex factory and material | A mesh owns multiple sections. Instancing is explicit and separate. |
| Bevy | `Mesh3d` entities, submeshes | Render phases batch items that share a pipeline and buffers | Batching is an internal render-phase concern. |
| Minecraft / Sodium | Chunk section renderers | One arena per render pass, section geometry in ranges, multi-draw and indirect draws | The closest model. This is the design to copy. |

Lessons:

1. Ranges (sections, surfaces, submeshes) are the minimal public abstraction.
2. Instancing is for identical meshes. It is not a batching mechanism for
   unique geometry.
3. The renderer owns the arena, the draw commands, and the culling.
4. Do not expose command buffers or arenas to gameplay code.

## 4. Requirements

- One draw call per material per pass for a batch of chunks.
- No GL object creation per chunk rebuild.
- Per-range frustum culling for the camera and for each shadow cascade.
- Back-to-front order for transparent ranges.
- One model matrix per batch. Chunk vertices are already in object space
  (`WaywardBeyond.Client/Voxels/Processing/MeshPostPass.cs:65, 230`).
- Bounded memory. A batch must not copy every live range on every edit.
- No locks on the render path. No per-frame allocation after warm-up.
- The game API stays small. The game never sees GL types, arenas, or command
  buffers.

## 5. Non-goals

- Order-independent transparency.
- GPU-driven culling in the first version.
- Merging different voxel objects into one batch. Each object can move
  independently.
- Replacing the instanced renderer. Instancing stays for repeated props.
- LOD.

## 6. Options

| Option | Draw calls | Rebuild cost | Memory | Complexity | Verdict |
|---|---|---|---|---|---|
| A. Per-chunk VAO (today) | `O(chunks * passes)` | New GL objects per edit | Low per chunk, high object count | Low | Current problem |
| B. Instanced chunks | No win | Worse | Low | Low | Rejected. Chunk meshes are unique. |
| C. Per-object merged buffers + multi-draw | `O(materials * passes)` | One range update | One arena per object | Medium | Recommended |
| D. Indirect draws + GPU culling | `O(materials * passes)` | One range update | One arena + command buffer | High | Later step |
| E. Region batches over several objects | `O(regions * passes)` | One range update | Shared arena | High | Later step for many small objects |
| F. One giant mesh per object, rebuilt whole | 1 per material | Full rebuild | One mesh | Low | Rejected. It restores the old rebuild cost. |

Option C is the right first step. It solves the draw count and the GL object
churn without a compute pipeline. Option D can replace the CPU cull later
without changing the game API.

## 7. Design

### 7.1 Engine types

Four public types and one render context method. That is the whole surface.
`MeshBatchWriter` is a helper. The other three are the contract.

```csharp
// Swordfish.Graphics
public sealed class MeshBatch : Handle
{
    public MeshBatch(int vertexCapacity = 4096, int indexCapacity = 8192);

    public MeshRangeHandle AddRange(
        ReadOnlySpan<float> vertexData,
        int vertexCount,
        ReadOnlySpan<uint> indices,
        in BoundingBox bounds
    );

    public void UpdateRange(
        MeshRangeHandle handle,
        ReadOnlySpan<float> vertexData,
        int vertexCount,
        ReadOnlySpan<uint> indices,
        in BoundingBox bounds
    );

    public void RemoveRange(MeshRangeHandle handle);
}

public readonly struct MeshRangeHandle
{
    public readonly int Slot;
    public readonly uint Version;
}

public sealed class MeshBatchRenderer : IHandle
{
    public MeshBatch MeshBatch { get; }
    public Material[] Materials { get; }
    public RenderOptions RenderOptions { get; }
}

// Swordfish.Graphics.IRenderContext
void Bind(MeshBatchRenderer renderer, Uuid entity);
```

`MeshBatchWriter` is an optional helper. It is a pooled CPU buffer with
`BeginRange`, `AddVertex`, `AddTriangle`, and `Commit`. The voxel mesh pass uses
it to write vertices and indices without building `List<T>` arrays or a `Mesh`
per chunk.

```csharp
public sealed class MeshBatchWriter
{
    public void BeginRange(int vertexCapacity, int indexCapacity);
    public void AddVertex(float positionX, float positionY, float positionZ, ...);
    public void AddTriangle(uint a, uint b, uint c);
    public MeshRangeHandle Commit(MeshBatch batch, in BoundingBox bounds);
}
```

The writer packs the current 13-float vertex layout. Do not introduce a vertex
type in the public API. The layout is already fixed by the shader attributes
(`Swordfish/IO/GlslParser.cs:94-98`).

The engine does not know about chunks, voxels, or `Short3`. The caller keeps
the mapping from its own key to `MeshRangeHandle`.

### 7.2 Buffer layout

- One vertex buffer and one index buffer per `MeshBatch`.
- The vertex layout is the current `Mesh` layout: position, color, uv, normal.
  Keep 13 floats per vertex so the existing shaders and attribute setup work.
- Each range owns `(vertexOffset, vertexCount, indexOffset, indexCount, bounds)`.
- Index values are range-local. The draw command adds `baseVertex` per range.
  This keeps each range independent and lets a range move without rewriting its
  indices.
- A batch covers one alpha mode and one material set. A voxel object uses three
  batches: opaque, masked, transparent. This keeps the draw state constant per
  batch.

### 7.3 Slot allocation

- Keep a free list of slots and a high-water mark.
- `UpdateRange`: if the new counts fit the slot, write in place and bump the
  version. Otherwise allocate a new slot and release the old one.
- Coalesce adjacent free slots. Compact the arena when the waste passes 25
  percent, or during a loading screen.
- Grow the buffer by 1.5x or 2x. On growth, re-upload the live ranges and
  rebuild the slot map. Growth is rare.
- Use `glBufferData` with a null pointer to orphan the buffer before a large
  upload, then `glBufferSubData`. This avoids a pipeline stall.

### 7.4 Renderer side

`GLMeshBatch` owns the VAO, VBO, IBO, and the range table. It lives on the GL
thread.

`GLMeshBatchRenderer` is a world-space render stage. It handles the camera pass,
the depth pass, and the shadow pass.

Per pass:

1. Build the pass frustum. Camera passes use `renderScene.Frustum`. Shadow
   passes use `Frustum.FromViewProjection(view * projection)`.
2. Walk the range table in order. Test each range AABB against the frustum. The
   AABB is object space. Transform it by the batch model matrix, or transform
   the frustum into object space once per pass. The second is cheaper.
3. Append visible ranges to a pooled command list.
4. For transparent passes, sort the command list by distance from the camera,
   far to near.
5. Upload the model matrices. One matrix per batch. Do not upload per range.
6. Draw with one `glMultiDrawElementsBaseVertex` per material.

The command list is a flat array of structs:

```csharp
struct RangeCommand
{
    public int IndexOffset;   // bytes into the IBO
    public int IndexCount;
    public int BaseVertex;
    public float DistanceSq;  // transparent only
}
```

No dictionaries. No LINQ. No per-frame allocation after warm-up.

### 7.5 Draw call

Use `glMultiDrawElementsBaseVertex`:

```csharp
_gl.MultiDrawElementsBaseVertex(
    PrimitiveType.Triangles,
    counts,        // int* or int[]
    DrawElementsType.UnsignedInt,
    offsets,       // void** into the bound IBO
    (uint)visibleCount,
    baseVertices   // int*
);
```

Silk.NET exposes the pointer variants. Pool the three arrays. Counts and offsets
change per pass, so rebuild them from the command list.

Why base vertex and not indirect:

- No command buffer upload per pass.
- Simpler lifetime.
- The CPU cull already knows the visible set.
- Indirect adds value only when a compute shader culls. Keep it as the next
  step.

### 7.6 The `model` matrix

`GlslParser` declares `layout (location = 4) in mat4 model;`
(`Swordfish/IO/GlslParser.cs:98`). The instanced renderer fills it from a
per-instance matrix buffer with divisor 1 (`GLRenderTarget.cs:41-46`).

A batch has one model matrix. Two options:

1. Upload one matrix to the batch matrix buffer and rely on instance 0 for the
   divisor-1 attributes. `glMultiDrawElementsBaseVertex` is not instanced, so
   every vertex reads element 0. This works on the target drivers but is
   subtle.
2. Add a `uModel` uniform variant to the shader prologue and use it for batched
   draws. This is explicit and portable.

Recommendation: option 1 for the first version, with a driver check. Switch to
option 2 if any driver misbehaves. Do not add per-vertex model data.

### 7.7 Threading and upload

- The game writes range data on the ECS thread. The data is immutable after
  `UpdateRange` or `Commit`.
- The batch keeps a per-range version counter.
- On the GL thread, at the start of the frame, compare versions and upload
  changed ranges with `BufferSubData`.
- Bind is queued through the existing unbound-renderer queue
  (`Swordfish/Graphics/UnboundRenderer.cs`). The queue holds `MeshRenderer`
  today. Generalize it to carry an `IHandle` and a bind kind, or add a second
  queue for batch renderers.
- Do not create or destroy GL objects from the ECS thread.
- Disposal follows the existing pattern: the `Disposed` event posts to the GL
  synchronization context (`GLRenderContext.cs:81-97`).

### 7.8 Memory

- One VBO and one IBO per batch, not per chunk.
- The CPU-side range payload can be released after upload if the game does not
  need to re-upload. For chunk rebuilds the payload is replaced on the next
  edit, so release it after upload.
- The range table is 32 bytes per range (offsets, counts, bounds).
- For 1,000 chunks: 32 KB of range metadata, one VBO, one IBO.
- The current per-chunk path costs 1,000 VAOs, 1,000 VBOs, 1,000 EBOs, 1,000
  MBOs, 1,000 `Mesh` objects, and up to 3,000 renderer entities.

### 7.9 Correctness cases

- A range is updated while the previous frame still references the old range.
  The GL thread applies updates before it builds the command list.
- A batch grows during an update. The GL thread re-uploads and rebuilds the
  range table before any draw.
- A range is removed. The command list must not include it. The slot is freed
  after the current frame.
- A batch is disposed. The render stage must drop it on the same frame.
- An empty batch. Skip the draw.
- Transparent ranges. Sort far to near. Ties keep a stable order.
- Culling. Use the object-space AABB transformed by the batch matrix, or the
  frustum transformed into object space. Do not mix spaces.
- Chunk bounds. Compute from the chunk coordinates and the object extents. Do
  not scan vertices.

## 8. Client integration

- `VoxelChunkRegistry` stores `MeshRangeHandle` per chunk and per alpha mode,
  next to the collision data.
- `VoxelEntityBuilder` creates three `MeshBatchRenderer` entities per voxel
  object and writes ranges into their batches.
- `BuildChunk` writes through `MeshBatchWriter`. It no longer creates `Mesh`
  objects or `MeshRenderer` entities.
- A geometry edit updates only the dirty chunks' ranges. A light-only edit can
  update the range color data only (see [0095](../issues/0095-voxel-light-geometry-split.md)).
- The per-chunk ECS entities and `VoxelChunkComponent` are removed.
- `MeshRendererCleanup` stays for non-chunk renderers. Chunk cleanup becomes
  `RemoveRange`.
- Transparent batches sort their commands by camera distance.

## 9. Performance expectation

Per voxel object.

| Case | Today | After batching |
|---|---|---|
| Visible chunks | N | N |
| Draw calls, color pass | N | 3 (one per alpha mode) |
| Draw calls, 3 cascades | 3N | 9 |
| Draw calls, 2 point shadows | 12N | 6 (layered) or 36 (per face) |
| GL objects per object | 4 per chunk | 3 batches |
| GL objects per edit | 4 per dirty chunk | 0 |
| Per-entity CPU work | N targets, hashing | 0 |
| Range culling | per entity AABB | per range AABB, flat array |

The remaining multiply is the shadow passes. Phase E2 removes most of it.

## 10. Migration

1. E6a: data model and tests. Dormant.
2. E6b: OpenGL stage and tests. Dormant behind a disabled registration.
3. E6c: lifetime, fallbacks, benchmark.
4. G6: switch voxel chunks to the batch renderer. Keep the per-chunk path behind
   a temporary feature flag for one release.
5. Remove the per-chunk path and the dead cleanup code.

## 11. Risks

| Risk | Mitigation |
|---|---|
| Multi-draw with divisor-1 model attributes misbehaves on a driver | Test the target GPU. Keep the `uModel` uniform variant ready. |
| Arena fragmentation over long edit sessions | Compaction with a 25 percent waste trigger. Benchmark a long edit loop. |
| Buffer growth causes a frame spike | Grow with 1.5x/2x. Compact during load. Measure the worst case. |
| Transparent sorting cost | Sort only visible ranges. Typical visible count is small. |
| Range update races with the render thread | Versioned immutable payloads. Upload at frame start. |
| A batch is used after disposal | Same `IHandle` lifetime contract as `MeshRenderer`. Test it. |
| The engine API grows too large | Keep three public types. Keep arenas and commands internal. |

## 12. Open questions

1. Does the target driver accept the divisor-1 model attribute for a
   non-instanced multi-draw? If not, add the `uModel` variant.
2. Do we need `glMultiDrawElementsIndirect` in the first version? Recommended:
   no. Add it with GPU culling.
3. Should several small voxel objects share a batch? Recommended: later. It
   needs per-range transforms or instancing.
4. What is the maximum chunks per object? The arena high-water mark should be
   sized from the object chunk count at build time.

## 13. References

- Sodium (Minecraft chunk renderer): per-pass geometry arenas, section ranges,
  visibility lists, multi-draw and indirect draws.
- Godot: `ArrayMesh` surfaces and `MultiMesh`.
- Unreal: static mesh sections and `UInstancedStaticMeshComponent`.
- Bevy: render phases and pipeline batching.
- Unity: SRP Batcher and dynamic batching.
- OpenGL 4.3: `glMultiDrawElementsBaseVertex`,
  `glMultiDrawElementsIndirect`.

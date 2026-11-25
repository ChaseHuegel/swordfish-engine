using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;
using DryIoc;
using Swordfish.Graphics;
using Swordfish.Library.Types.Shapes;
using WaywardBeyond.Client.Voxels.Models;
using WaywardBeyond.Voxels;
using WaywardBeyond.Client.Voxels.Processing;

namespace WaywardBeyond.Client.Voxels.Building;

public sealed class VoxelObjectBuilder(in IContainer container)
{
    private readonly IContainer _container = container;
    private readonly ConcurrentBag<ChunkBuildContext> _contextPool = [];

    /// <summary>Opens one resolver scope for a sequence of chunk builds.</summary>
    public ChunkBuildContext OpenChunkBuildContext()
    {
        if (_contextPool.TryTake(out ChunkBuildContext? context))
        {
            context.PrepareReuse();
            return context;
        }

        return new ChunkBuildContext(_container.OpenScope(), this);
    }

    /// <summary>Builds mesh data and voxel entities for a single chunk.</summary>
    public ChunkMeshData BuildChunk(ChunkBuildContext context, ChunkData chunkData)
    {
        context.MeshState.Clear();
        context.EntityState.Clear();

        if (!chunkData.Palette.Only(0))
        {
            foreach (VoxelSample sample in chunkData.GetOccupiedSampler())
            {
                context.MeshPostPass.Process(sample);
                context.VoxelEntityPostPass.Process(sample);
            }
        }

        ChunkMesh opaqueMesh = GenerateChunkMesh(context.MeshState.Opaque);
        ChunkMesh opaqueNoShadowMesh = GenerateChunkMesh(context.MeshState.OpaqueNoShadow);
        ChunkMesh maskedMesh = GenerateChunkMesh(context.MeshState.Masked);
        ChunkMesh transparentMesh = GenerateChunkMesh(context.MeshState.Transparent);

        return new ChunkMeshData(opaqueMesh, opaqueNoShadowMesh, maskedMesh, transparentMesh, context.EntityState.Voxels);
    }

    /// <summary>Builds surface collision data for a single chunk.</summary>
    public ChunkCollisionData BuildChunkCollision(ChunkBuildContext context, ChunkData chunkData)
    {
        context.CollisionState.Clear();
        context.CollisionPostPass.ProcessChunk(chunkData);

        if (context.CollisionState.Shapes.Count == 0)
        {
            return new ChunkCollisionData([], [], []);
        }

        return new ChunkCollisionData(
            shapes: [.. context.CollisionState.Shapes],
            positions: [.. context.CollisionState.Positions],
            orientations: [.. context.CollisionState.Orientations]
        );
    }

    /// <summary>Holds one resolver scope and the passes shared across chunk builds.</summary>
    public sealed class ChunkBuildContext : IDisposable
    {
        private readonly IResolverContext _scope;
        private readonly VoxelObjectBuilder _owner;
        private int _returned;

        internal readonly MeshPostPass MeshPostPass;
        internal readonly VoxelEntityPostPass VoxelEntityPostPass;
        internal readonly CollisionPostPass CollisionPostPass;
        internal readonly MeshState MeshState;
        internal readonly EntityState EntityState;
        internal readonly CollisionState CollisionState;

        internal ChunkBuildContext(IResolverContext scope, VoxelObjectBuilder owner)
        {
            _scope = scope;
            _owner = owner;
            MeshPostPass = scope.Resolve<MeshPostPass>();
            VoxelEntityPostPass = scope.Resolve<VoxelEntityPostPass>();
            CollisionPostPass = scope.Resolve<CollisionPostPass>();
            MeshState = scope.Resolve<MeshState>();
            EntityState = scope.Resolve<EntityState>();
            CollisionState = scope.Resolve<CollisionState>();
        }

        internal void PrepareReuse()
        {
            MeshState.Clear();
            EntityState.Clear();
            CollisionState.Clear();
            Volatile.Write(ref _returned, 0);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _returned, 1) == 0)
            {
                MeshState.Clear();
                EntityState.Clear();
                CollisionState.Clear();
                _owner._contextPool.Add(this);
            }
        }
    }

    /// <summary>Combines cached chunk collision shapes into a single compound shape.</summary>
    public static CompoundShape CombineCollisions(IReadOnlyCollection<ChunkCollisionData> chunkColliders)
    {
        var totalShapes = 0;
        foreach (ChunkCollisionData chunk in chunkColliders)
        {
            totalShapes += chunk.Shapes.Length;
        }

        var shapes = new Shape[totalShapes];
        var positions = new Vector3[totalShapes];
        var orientations = new Quaternion[totalShapes];

        var offset = 0;
        foreach (ChunkCollisionData chunk in chunkColliders)
        {
            int count = chunk.Shapes.Length;
            if (count == 0)
            {
                continue;
            }

            Array.Copy(chunk.Shapes, 0, shapes, offset, count);
            Array.Copy(chunk.Positions, 0, positions, offset, count);
            Array.Copy(chunk.Orientations, 0, orientations, offset, count);
            offset += count;
        }

        return new CompoundShape(shapes, positions, orientations);
    }

    /// <summary>Initializes ambient and direct lighting for the voxel object.</summary>
    public void InitializeLighting(VoxelObject voxelObject, DepthState? depthState = null)
    {
        using IResolverContext? scope = _container.OpenScope();
        if (depthState != null)
        {
            scope.Use(depthState);
        }
        
        var voxelObjectProcessor = scope.Resolve<VoxelObjectProcessor>();
        voxelObjectProcessor.ProcessLighting(voxelObject);
    }

    private static ChunkMesh GenerateChunkMesh(MeshState.MeshData meshData)
    {
        int triCount = meshData.Triangles.Count;
        int count = meshData.Vertices.Count;
        if (triCount == 0 || count == 0)
        {
            return default;
        }

        int requiredFloats = count * MeshBatch.VERTEX_STRIDE;
        if (meshData.VertexBuffer.Length < requiredFloats)
        {
            meshData.VertexBuffer = new float[Math.Max(requiredFloats, meshData.VertexBuffer.Length * 2)];
        }

        if (meshData.IndexBuffer.Length < triCount)
        {
            meshData.IndexBuffer = new uint[Math.Max(triCount, meshData.IndexBuffer.Length * 2)];
        }

        float[] vertexData = meshData.VertexBuffer;
        Span<Vector3> vertices = CollectionsMarshal.AsSpan(meshData.Vertices);
        Span<Vector4> colors = CollectionsMarshal.AsSpan(meshData.Colors);
        Span<Vector3> uvs = CollectionsMarshal.AsSpan(meshData.UV);
        Span<Vector3> normals = CollectionsMarshal.AsSpan(meshData.Normals);

        Vector3 min = vertices[0];
        Vector3 max = vertices[0];

        for (var i = 0; i < count; i++)
        {
            Vector3 position = vertices[i];
            min = Vector3.Min(min, position);
            max = Vector3.Max(max, position);

            Vector4 color = colors[i];
            Vector3 uv = uvs[i];
            Vector3 normal = normals[i];

            int offset = i * MeshBatch.VERTEX_STRIDE;
            vertexData[offset] = position.X;
            vertexData[offset + 1] = position.Y;
            vertexData[offset + 2] = position.Z;
            vertexData[offset + 3] = color.X;
            vertexData[offset + 4] = color.Y;
            vertexData[offset + 5] = color.Z;
            vertexData[offset + 6] = color.W;
            vertexData[offset + 7] = uv.X;
            vertexData[offset + 8] = uv.Y;
            vertexData[offset + 9] = uv.Z;
            vertexData[offset + 10] = normal.X;
            vertexData[offset + 11] = normal.Y;
            vertexData[offset + 12] = normal.Z;
        }

        Vector3 center = (min + max) * 0.5f;
        Vector3 extents = (max - min) * 0.5f;
        var bounds = new BoundingBox(center, extents, extents.Length());

        CollectionsMarshal.AsSpan(meshData.Triangles).CopyTo(meshData.IndexBuffer);
        return new ChunkMesh(vertexData, count, meshData.IndexBuffer, triCount, bounds);
    }

    /// <summary>Holds opaque, non-shadow-casting opaque, masked, and transparent mesh ranges and entities for a single chunk.</summary>
    public readonly struct ChunkMeshData(in ChunkMesh opaqueMesh, in ChunkMesh opaqueNoShadowMesh, in ChunkMesh maskedMesh, in ChunkMesh transparentMesh, in List<VoxelInfo> voxelEntities)
    {
        public readonly ChunkMesh OpaqueMesh = opaqueMesh;
        public readonly ChunkMesh OpaqueNoShadowMesh = opaqueNoShadowMesh;
        public readonly ChunkMesh MaskedMesh = maskedMesh;
        public readonly ChunkMesh TransparentMesh = transparentMesh;
        public readonly List<VoxelInfo> VoxelEntities = voxelEntities;
    }

    /// <summary>Holds packed vertex and index data for one chunk range. Pooled buffers are only valid until the next <see cref="BuildChunk"/> on the same context.</summary>
    public readonly struct ChunkMesh
    {
        public readonly float[] VertexData;
        public readonly uint[] Indices;
        public readonly int VertexCount;
        public readonly int IndexCount;
        public readonly BoundingBox Bounds;

        /// <summary>Wraps owned vertex and index arrays whose full lengths define the element counts.</summary>
        public ChunkMesh(in float[] vertexData, in uint[] indices, in BoundingBox bounds)
        {
            VertexData = vertexData;
            Indices = indices;
            VertexCount = vertexData == null ? 0 : vertexData.Length / MeshBatch.VERTEX_STRIDE;
            IndexCount = indices == null ? 0 : indices.Length;
            Bounds = bounds;
        }

        /// <summary>Wraps pooled vertex and index buffers with explicit active <paramref name="vertexCount"/> and <paramref name="indexCount"/>.</summary>
        public ChunkMesh(float[] vertexData, int vertexCount, uint[] indices, int indexCount, in BoundingBox bounds)
        {
            VertexData = vertexData;
            VertexCount = vertexCount;
            Indices = indices;
            IndexCount = indexCount;
            Bounds = bounds;
        }

        public bool IsEmpty => VertexCount == 0 || IndexCount == 0 || VertexData == null || Indices == null;
    }
}
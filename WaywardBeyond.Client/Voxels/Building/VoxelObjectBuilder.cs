using System;
using System.Collections.Generic;
using System.Numerics;
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

    /// <summary>Opens one resolver scope for a sequence of chunk builds.</summary>
    public ChunkBuildContext OpenChunkBuildContext()
    {
        return new ChunkBuildContext(_container.OpenScope());
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

        float half = chunkData.Data.Size * 0.5f;
        var chunkBounds = new BoundingBox(
            new Vector3(
                chunkData.Coords.X * chunkData.Data.Size + half,
                chunkData.Coords.Y * chunkData.Data.Size + half,
                chunkData.Coords.Z * chunkData.Data.Size + half
            ),
            new Vector3(half + 1f),
            (half + 1f) * MathF.Sqrt(3f)
        );

        Mesh? opaqueMesh = GenerateMeshOrNull(context.MeshState.Opaque, chunkBounds);
        Mesh? maskedMesh = GenerateMeshOrNull(context.MeshState.Masked, chunkBounds);
        Mesh? transparentMesh = GenerateMeshOrNull(context.MeshState.Transparent, chunkBounds);

        return new ChunkMeshData(opaqueMesh, maskedMesh, transparentMesh, context.EntityState.Voxels);
    }

    /// <summary>Builds surface collision data for a single chunk.</summary>
    public ChunkCollisionData BuildChunkCollision(ChunkBuildContext context, ChunkData chunkData)
    {
        context.CollisionState.Clear();

        if (!context.CollisionPostPass.ShouldProcessChunk(chunkData))
        {
            return new ChunkCollisionData([], [], []);
        }

        foreach (VoxelSample sample in chunkData.GetOccupiedSampler())
        {
            context.CollisionPostPass.Process(sample);
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

        internal readonly MeshPostPass MeshPostPass;
        internal readonly VoxelEntityPostPass VoxelEntityPostPass;
        internal readonly CollisionPostPass CollisionPostPass;
        internal readonly MeshState MeshState;
        internal readonly EntityState EntityState;
        internal readonly CollisionState CollisionState;

        internal ChunkBuildContext(IResolverContext scope)
        {
            _scope = scope;
            MeshPostPass = scope.Resolve<MeshPostPass>();
            VoxelEntityPostPass = scope.Resolve<VoxelEntityPostPass>();
            CollisionPostPass = scope.Resolve<CollisionPostPass>();
            MeshState = scope.Resolve<MeshState>();
            EntityState = scope.Resolve<EntityState>();
            CollisionState = scope.Resolve<CollisionState>();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _scope.Dispose();
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

    private static Mesh? GenerateMeshOrNull(in MeshState.MeshData meshData, in BoundingBox bounds)
    {
        if (meshData.Triangles.Count == 0)
        {
            return null;
        }

        return new Mesh(
            meshData.Triangles.ToArray(),
            meshData.Vertices.ToArray(),
            meshData.Colors.ToArray(),
            meshData.UV.ToArray(),
            meshData.Normals.ToArray(),
            bounds
        );
    }

    /// <summary>Holds opaque, masked, and transparent meshes and entities for a single chunk.</summary>
    public readonly struct ChunkMeshData(in Mesh? opaqueMesh, in Mesh? maskedMesh, in Mesh? transparentMesh, in List<VoxelInfo> voxelEntities)
    {
        public readonly Mesh? OpaqueMesh = opaqueMesh;
        public readonly Mesh? MaskedMesh = maskedMesh;
        public readonly Mesh? TransparentMesh = transparentMesh;
        public readonly List<VoxelInfo> VoxelEntities = voxelEntities;
    }
}
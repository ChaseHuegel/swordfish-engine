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
    
    public Data Build(VoxelObject voxelObject)
    {
        using IResolverContext? scope = _container.OpenScope();
        var voxelObjectProcessor = scope.Resolve<VoxelObjectProcessor>();
        voxelObjectProcessor.Process(voxelObject);
        
        var meshState = scope.Resolve<MeshState>();
        Mesh opaqueMesh = GenerateMeshOrNull(meshState.Opaque) ?? new Mesh([], [], [], [], []);
        Mesh maskedMesh = GenerateMeshOrNull(meshState.Masked) ?? new Mesh([], [], [], [], []);
        Mesh transparentMesh = GenerateMeshOrNull(meshState.Transparent) ?? new Mesh([], [], [], [], []);
        
        var collisionState = scope.Resolve<CollisionState>();
        var collisionShape = new CompoundShape(collisionState.Shapes.ToArray(), collisionState.Positions.ToArray(), collisionState.Orientations.ToArray());
        
        var entityState = scope.Resolve<EntityState>();
        
        return new Data(opaqueMesh, maskedMesh, transparentMesh, collisionShape, entityState.Voxels);
    }
    
    /// <summary>Builds mesh data and voxel entities for a single chunk.</summary>
    public ChunkMeshData BuildChunk(ChunkData chunkData)
    {
        using IResolverContext? scope = _container.OpenScope();
        var meshPostPass = scope.Resolve<MeshPostPass>();
        var voxelEntityPostPass = scope.Resolve<VoxelEntityPostPass>();
        var meshState = scope.Resolve<MeshState>();
        var entityState = scope.Resolve<EntityState>();

        if (!chunkData.Palette.Only(0))
        {
            foreach (VoxelSample sample in chunkData.GetOccupiedSampler())
            {
                meshPostPass.Process(sample);
                voxelEntityPostPass.Process(sample);
            }
        }

        Mesh? opaqueMesh = GenerateMeshOrNull(meshState.Opaque);
        Mesh? maskedMesh = GenerateMeshOrNull(meshState.Masked);
        Mesh? transparentMesh = GenerateMeshOrNull(meshState.Transparent);

        return new ChunkMeshData(opaqueMesh, maskedMesh, transparentMesh, entityState.Voxels);
    }

    /// <summary>Builds surface collision data for a single chunk.</summary>
    public ChunkCollisionData BuildChunkCollision(ChunkData chunkData)
    {
        using IResolverContext? scope = _container.OpenScope();
        var collisionPass = scope.Resolve<CollisionPostPass>();
        var collisionState = scope.Resolve<CollisionState>();

        if (!collisionPass.ShouldProcessChunk(chunkData))
        {
            return new ChunkCollisionData([], [], []);
        }

        foreach (VoxelSample sample in chunkData.GetOccupiedSampler())
        {
            collisionPass.Process(sample);
        }

        return new ChunkCollisionData(
            shapes: [.. collisionState.Shapes],
            positions: [.. collisionState.Positions],
            orientations: [.. collisionState.Orientations]
        );
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

    /// <summary>Builds a surface collision shape for the voxel object.</summary>
    public CompoundShape BuildCollision(VoxelObject voxelObject)
    {
        var colliders = new List<ChunkCollisionData>(voxelObject.ChunkCount);
        foreach (ChunkData chunk in voxelObject)
        {
            ChunkCollisionData chunkCollision = BuildChunkCollision(chunk);
            if (!chunkCollision.IsEmpty)
            {
                colliders.Add(chunkCollision);
            }
        }

        return CombineCollisions(colliders);
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

    private static Mesh? GenerateMeshOrNull(in MeshState.MeshData meshData)
    {
        if (meshData.Triangles.Count == 0)
        {
            return null;
        }

        return new Mesh(meshData.Triangles.ToArray(), meshData.Vertices.ToArray(), meshData.Colors.ToArray(), meshData.UV.ToArray(), meshData.Normals.ToArray());
    }

    /// <summary>Holds opaque, masked, and transparent meshes and entities for a single chunk.</summary>
    public readonly struct ChunkMeshData(in Mesh? opaqueMesh, in Mesh? maskedMesh, in Mesh? transparentMesh, in List<VoxelInfo> voxelEntities)
    {
        public readonly Mesh? OpaqueMesh = opaqueMesh;
        public readonly Mesh? MaskedMesh = maskedMesh;
        public readonly Mesh? TransparentMesh = transparentMesh;
        public readonly List<VoxelInfo> VoxelEntities = voxelEntities;
    }
    
    public readonly struct Data(in Mesh opaqueMesh, in Mesh maskedMesh, in Mesh transparentMesh, in CompoundShape collisionShape, in List<VoxelInfo> voxelEntities)
    {
        public readonly Mesh OpaqueMesh = opaqueMesh;
        public readonly Mesh MaskedMesh = maskedMesh;
        public readonly Mesh TransparentMesh = transparentMesh;
        public readonly CompoundShape CollisionShape = collisionShape;
        public readonly List<VoxelInfo> VoxelEntities = voxelEntities;
    }
}
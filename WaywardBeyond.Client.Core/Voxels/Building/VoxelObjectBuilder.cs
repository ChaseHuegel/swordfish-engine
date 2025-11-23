using System.Collections.Generic;
using DryIoc;
using Swordfish.Graphics;
using Swordfish.Library.Types.Shapes;
using WaywardBeyond.Client.Core.Voxels.Models;
using WaywardBeyond.Client.Core.Voxels.Processing;

namespace WaywardBeyond.Client.Core.Voxels.Building;

public sealed class VoxelObjectBuilder(in IContainer container)
{
    private readonly IContainer _container = container;
    
    public Data Build(VoxelObject voxelObject)
    {
        using IResolverContext? scope = _container.OpenScope();
        var voxelObjectProcessor = scope.Resolve<VoxelObjectProcessor>();
        voxelObjectProcessor.Process(voxelObject);
        
        var meshState = scope.Resolve<MeshState>();
        Mesh opaqueMesh = GenerateMesh(meshState.Opaque);
        Mesh transparentMesh = GenerateMesh(meshState.Transparent);
        
        var collisionState = scope.Resolve<CollisionState>();
        var collisionShape = new CompoundShape(collisionState.Shapes.ToArray(), collisionState.Positions.ToArray(), collisionState.Orientations.ToArray());
        
        var entityState = scope.Resolve<EntityState>();
        
        return new Data(opaqueMesh, transparentMesh, collisionShape, entityState.Voxels);
    }
    
    private static Mesh GenerateMesh(in MeshState.MeshData meshData)
    {
        return new Mesh(meshData.Triangles.ToArray(), meshData.Vertices.ToArray(), meshData.Colors.ToArray(), meshData.UV.ToArray(), meshData.Normals.ToArray());
    }
    
    public readonly struct Data(in Mesh opaqueMesh, in Mesh transparentMesh, in CompoundShape collisionShape, in List<VoxelInfo> voxelEntities)
    {
        public readonly Mesh OpaqueMesh = opaqueMesh;
        public readonly Mesh TransparentMesh = transparentMesh;
        public readonly CompoundShape CollisionShape = collisionShape;
        public readonly List<VoxelInfo> VoxelEntities = voxelEntities;
    }
}
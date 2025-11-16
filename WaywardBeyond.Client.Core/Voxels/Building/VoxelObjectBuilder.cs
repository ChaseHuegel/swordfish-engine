using System.Collections.Generic;
using DryIoc;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.Types.Shapes;
using WaywardBeyond.Client.Core.Voxels.Processing;

namespace WaywardBeyond.Client.Core.Voxels.Building;

internal sealed class VoxelObjectBuilder(in IContainer container)
{
    private readonly IContainer _container = container;
    
    public Data Build(DataStore store, int entity, VoxelObject voxelObject, bool transparent = false)
    {
        using IResolverContext? scope = _container.OpenScope();
        var voxelObjectProcessor = scope.Resolve<VoxelObjectProcessor>();
        voxelObjectProcessor.Process(voxelObject);
        
        var meshState = scope.Resolve<MeshState>();
        MeshState.MeshData meshData = transparent ? meshState.Transparent : meshState.Opaque;
        var mesh = new Mesh(meshData.Triangles.ToArray(), meshData.Vertices.ToArray(), meshData.Colors.ToArray(), meshData.UV.ToArray(), meshData.Normals.ToArray());
        
        var collisionState = scope.Resolve<CollisionState>();
        var collisionShape = new CompoundShape(collisionState.Shapes.ToArray(), collisionState.Positions.ToArray(), collisionState.Orientations.ToArray());

        var lightingState = scope.Resolve<LightingState>();
        
        return new Data(mesh, collisionShape, lightingState.Sources);
    }
    
    public readonly struct Data(in Mesh mesh, in CompoundShape collisionShape, in List<LightingState.LightSource> lightSources)
    {
        public readonly Mesh Mesh = mesh;
        public readonly CompoundShape CollisionShape = collisionShape;
        public readonly List<LightingState.LightSource> LightSources = lightSources;
    }
}
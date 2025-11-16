using System.Numerics;
using DryIoc;
using Swordfish.ECS;
using Swordfish.Graphics;
using WaywardBeyond.Client.Core.Voxels.Processing;

namespace WaywardBeyond.Client.Core.Voxels.Building;

internal sealed class VoxelObjectBuilder(in IContainer container)
{
    private readonly IContainer _container = container;
    
    public Data Build(DataStore store, int entity, VoxelObject voxelObject, bool transparent = false)
    {
        //  TODO implement passes for managing entities (ie. lights)
        
        using IResolverContext? scope = _container.OpenScope();
        var voxelObjectProcessor = scope.Resolve<VoxelObjectProcessor>();
        voxelObjectProcessor.Process(voxelObject);

        var meshState = scope.Resolve<MeshState>();
        MeshState.MeshData meshData = transparent ? meshState.Transparent : meshState.Opaque;
        var mesh = new Mesh(meshData.Triangles.ToArray(), meshData.Vertices.ToArray(), meshData.Colors.ToArray(), meshData.UV.ToArray(), meshData.Normals.ToArray());

        //  TODO implement collision data pass
        return new Data(mesh, []);
    }

    public readonly struct Data(in Mesh mesh, in Vector3[] collisionPoints)
    {
        public readonly Mesh Mesh = mesh;
        public readonly Vector3[] CollisionPoints = collisionPoints;
    }
}
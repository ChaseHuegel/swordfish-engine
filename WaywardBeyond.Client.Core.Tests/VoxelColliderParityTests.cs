using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using Swordfish.Library.Types.Shapes;
using WaywardBeyond.Client.Core.Voxels;
using WaywardBeyond.Client.Core.Voxels.Models;
using WaywardBeyond.Client.Core.Voxels.Processing;
using WaywardBeyond.Shared.Data;
using WaywardBeyond.Shared.Gameplay;

namespace WaywardBeyond.Client.Core.Tests;

/// <summary>
/// Guards that the shared <see cref="VoxelColliderBuilder"/> (used by the server authority world) derives
/// the exact same collision geometry as the client render pipeline's <see cref="CollisionPostPass"/>. If
/// these diverge, the server's colliders and the client's prediction colliders disagree and reconcile can
/// yank the player through terrain. Includes negative chunk offsets, which the shared decoder must mirror
/// (VoxelObject stores negative coordinates differently than positive ones).
/// </summary>
public class VoxelColliderParityTests
{
    [Test]
    public void SharedColliderMatchesClientPostPass_SimpleWorld()
    {
        var voxelObject = new VoxelObject(chunkSize: 16);
        voxelObject.Set(0, 0, 0, new Voxel(1, 0, 0));
        voxelObject.Set(1, 0, 0, new Voxel(2, 0, 0));
        voxelObject.Set(0, 1, 0, new Voxel(3, 0, 0));
        voxelObject.Set(15, 15, 15, new Voxel(4, 0, 0));
        voxelObject.Set(16, 16, 16, new Voxel(5, 0, 0));

        AssertParity(voxelObject);
    }

    [Test]
    public void SharedColliderMatchesClientPostPass_NegativeChunkOffsets()
    {
        var voxelObject = new VoxelObject(chunkSize: 16);
        voxelObject.Set(-1, 0, 0, new Voxel(1, 0, 0));
        voxelObject.Set(-16, -1, -1, new Voxel(2, 0, 0));
        voxelObject.Set(-17, 0, 0, new Voxel(3, 0, 0));
        voxelObject.Set(0, -1, 0, new Voxel(4, 0, 0));
        voxelObject.Set(-20, 7, 3, new Voxel(5, 0, 0));

        AssertParity(voxelObject);
    }

    [Test]
    public void SharedColliderMatchesClientPostPass_SparseAcrossChunks()
    {
        var voxelObject = new VoxelObject(chunkSize: 16);
        for (var i = 0; i < 40; i++)
        {
            int x = i * 7 % 53 - 26;
            int y = i * 3 % 17 - 8;
            int z = i * 11 % 31 - 15;
            voxelObject.Set(x, y, z, new Voxel((ushort)(i + 1), 0, 0));
        }

        AssertParity(voxelObject);
    }

    private static void AssertParity(VoxelObject voxelObject)
    {
        var collisionState = new CollisionState();
        var processor = new VoxelObjectProcessor(
            passes: [],
            voxelPasses: [],
            samplePasses: [new CollisionPostPass(collisionState)]
        );
        processor.Process(voxelObject);

        CompoundShape shared = VoxelColliderBuilder.BuildCollition(voxelObject.GetChunkInfos());
        HashSet<(Vector3 position, Vector3 extents)> clientShapes = BuildShapeSet(
            collisionState.Shapes, collisionState.Positions, collisionState.Orientations
        );
        HashSet<(Vector3 position, Vector3 extents)> sharedShapes = BuildShapeSet(
            shared.Shapes, shared.Positions, shared.Orientations
        );

        Assert.That(sharedShapes.Count, Is.EqualTo(clientShapes.Count),
            "Server and client should derive the same number of collision boxes.");
        Assert.That(sharedShapes.SetEquals(clientShapes), Is.True,
            "Server and client collision geometry diverged.");
    }

    private static HashSet<(Vector3, Vector3)> BuildShapeSet(
        IReadOnlyCollection<Shape> shapes,
        IReadOnlyCollection<Vector3> positions,
        IReadOnlyCollection<Quaternion> orientations
    ) {
        var result = new HashSet<(Vector3, Vector3)>();
        List<Shape> shapeList = shapes.ToList();
        List<Vector3> positionList = positions.ToList();

        for (var i = 0; i < shapeList.Count; i++)
        {
            Shape shape = shapeList[i];
            if (shape.Type != ShapeType.Box3)
            {
                continue;
            }

            result.Add((positionList[i], shape.Box3.Extents));
        }

        Assert.That(result.Count, Is.EqualTo(shapeList.Count),
            "Only unit Box3 shapes are expected in collision geometry.");
        return result;
    }
}
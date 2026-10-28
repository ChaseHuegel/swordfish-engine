using WaywardBeyond.Data;
using WaywardBeyond.Voxels;
using Xunit;

namespace Swordfish.Tests;

public class ChunkOccupancyTests
{
    private static Voxel Stone => new(_ID: 1, _ShapeLight: new ShapeLight(default, 0), _Orientation: 0);
    private static Voxel Air => new(_ID: 0, _ShapeLight: new ShapeLight(default, 0), _Orientation: 0);

    private static int CountOccupied(VoxelObject voxelObject)
    {
        var count = 0;
        foreach (ChunkData chunk in voxelObject)
        {
            foreach (VoxelSample _ in chunk.GetOccupiedSampler())
            {
                count++;
            }
        }

        return count;
    }

    [Fact]
    public void SparseChunkVisitsOnlyNonEmptyVoxels()
    {
        var voxelObject = new VoxelObject(16);
        voxelObject.Set(2, 3, 4, Stone);
        voxelObject.Set(10, 10, 10, Stone);

        Assert.Equal(2, CountOccupied(voxelObject));
    }

    [Fact]
    public void ClearingAVoxelUpdatesAnAlreadyBuiltMask()
    {
        var voxelObject = new VoxelObject(16);
        voxelObject.Set(2, 3, 4, Stone);
        voxelObject.Set(10, 10, 10, Stone);
        Assert.Equal(2, CountOccupied(voxelObject));

        voxelObject.Set(2, 3, 4, Air);
        Assert.Equal(1, CountOccupied(voxelObject));
    }

    [Fact]
    public void DenseChunkVisitsEveryVoxel()
    {
        var voxelObject = new VoxelObject(16);
        for (int x = 0; x < 16; x++)
        for (int y = 0; y < 16; y++)
        for (int z = 0; z < 16; z++)
        {
            voxelObject.Set(x, y, z, Stone);
        }

        Assert.Equal(16 * 16 * 16, CountOccupied(voxelObject));
    }

    [Fact]
    public void EmptyChunkVisitsNothing()
    {
        var voxelObject = new VoxelObject(16);
        voxelObject.Set(8, 8, 8, Air);

        Assert.Equal(0, CountOccupied(voxelObject));
    }
}

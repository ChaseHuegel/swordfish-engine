using WaywardBeyond.Client.Voxels;
using WaywardBeyond.Voxels;
using Xunit;

namespace Swordfish.Tests;

public class VoxelChunkDirtyKindTests
{
    [Fact]
    public void LightOnlyMarkDoesNotMarkGeometry()
    {
        var registry = new VoxelChunkRegistry();
        var coords = new Short3(0, 0, 0);

        registry.MarkVoxelModified(8, 8, 8, chunkSize: 16, lightOnly: true);

        Assert.Contains(coords, registry.DirtyChunks);
        Assert.Contains(coords, registry.LightDirtyChunks);
        Assert.DoesNotContain(coords, registry.GeometryDirtyChunks);
    }

    [Fact]
    public void GeometryMarkClearsTheLightOnlyKindForTheChunk()
    {
        var registry = new VoxelChunkRegistry();
        var coords = new Short3(0, 0, 0);

        registry.MarkVoxelModified(8, 8, 8, chunkSize: 16, lightOnly: true);
        registry.MarkVoxelModified(8, 8, 8, chunkSize: 16);

        Assert.Contains(coords, registry.DirtyChunks);
        Assert.Contains(coords, registry.GeometryDirtyChunks);
        Assert.DoesNotContain(coords, registry.LightDirtyChunks);
    }

    [Fact]
    public void BorderMarkAlsoMarksTheOrthogonalNeighborAsTheSameKind()
    {
        var registry = new VoxelChunkRegistry();
        var neighbor = new Short3(-1, 0, 0);

        registry.MarkVoxelModified(0, 8, 8, chunkSize: 16, lightOnly: true);

        Assert.Contains(neighbor, registry.LightDirtyChunks);
        Assert.DoesNotContain(neighbor, registry.GeometryDirtyChunks);
    }
}

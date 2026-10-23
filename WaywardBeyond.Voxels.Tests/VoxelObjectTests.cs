using System;
using System.Collections.Generic;
using System.Numerics;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;

namespace WaywardBeyond.Voxels.Tests;

/// <summary>Pins the chunked container: addressing, sampling, and the snapshot boundary.</summary>
public class VoxelObjectTests
{
    public static IEnumerable<object[]> ChunkSizes()
    {
        yield return [(byte)8];
        yield return [(byte)16];
        yield return [(byte)32];
    }

    [Theory]
    [MemberData(nameof(ChunkSizes))]
    public void SetAndGetRoundTripAcrossChunks(byte chunkSize)
    {
        var voxels = new VoxelObject(chunkSize);
        int last = chunkSize - 1;
        int[] coordinates = [0, last, chunkSize, -1, -chunkSize, -chunkSize - 1];

        for (var i = 0; i < coordinates.Length; i++)
        {
            int coordinate = coordinates[i];
            voxels.Set(coordinate, coordinate, coordinate, new Voxel((ushort)(i + 1), 0, 0));
        }

        for (var i = 0; i < coordinates.Length; i++)
        {
            int coordinate = coordinates[i];
            Assert.Equal((ushort)(i + 1), voxels.Get(coordinate, coordinate, coordinate).ID);
        }

        Assert.Equal(0, voxels.Get(last, last, 0).ID);
    }

    [Theory]
    [MemberData(nameof(ChunkSizes))]
    public void GetOutsideAnyChunkReturnsTheLitEmptyVoxel(byte chunkSize)
    {
        var voxels = new VoxelObject(chunkSize);

        Voxel voxel = voxels.Get(0, 0, 0);

        Assert.Equal(0, voxel.ID);
        Assert.Equal(15, voxel.GetLightLevel());
    }

    [Fact]
    public void SetOverwritesAPopulatedVoxel()
    {
        var voxels = new VoxelObject(4);
        voxels.Set(1, 1, 1, new Voxel(1, 0, 0));

        voxels.Set(1, 1, 1, new Voxel(2, 0, 0));

        Assert.Equal(2, voxels.Get(1, 1, 1).ID);
    }

    [Fact]
    public void SnapshotDoesNotAliasTheContainer()
    {
        var voxels = new VoxelObject(4);
        voxels.Set(1, 1, 1, new Voxel(1, 0, 0));

        ChunkInfo[] snapshot = voxels.GetChunkInfos();
        voxels.Set(1, 1, 1, new Voxel(2, 0, 0));

        Assert.Equal(1, snapshot[0].Chunk.Voxels[SampleTestIndex.LocalIndex(1, 1, 1, chunkSize: 4)].ID);
        Assert.Equal(2, voxels.Get(1, 1, 1).ID);
    }

    [Fact]
    public void SampleReachesTheNeighbourInThePreviousChunk()
    {
        var voxels = new VoxelObject(4);
        voxels.Set(-1, 0, 0, new Voxel(7, 0, 0));
        voxels.Set(0, 0, 0, new Voxel(1, 0, 0));

        VoxelSample sample = voxels.Sample(0, 0, 0);

        Assert.Equal(7, sample.Left.ID);
    }

    [Fact]
    public void SampleAgreesWithTheSamplerForAnUnrotatedVoxel()
    {
        var voxels = new VoxelObject(4);
        voxels.Set(1, 1, 1, new Voxel(1, 0, 0));
        voxels.Set(2, 1, 1, new Voxel(2, 0, 0));
        voxels.Set(0, 1, 1, new Voxel(3, 0, 0));
        voxels.Set(1, 2, 1, new Voxel(4, 0, 0));
        voxels.Set(1, 0, 1, new Voxel(5, 0, 0));
        voxels.Set(1, 1, 2, new Voxel(6, 0, 0));
        voxels.Set(1, 1, 0, new Voxel(7, 0, 0));

        VoxelSample sampled = voxels.Sample(1, 1, 1);
        VoxelSample iterated = SampleAt(voxels, 1, 1, 1);

        Assert.Equal(iterated.Left.ID, sampled.Left.ID);
        Assert.Equal(iterated.Right.ID, sampled.Right.ID);
        Assert.Equal(iterated.Above.ID, sampled.Above.ID);
        Assert.Equal(iterated.Below.ID, sampled.Below.ID);
        Assert.Equal(iterated.Ahead.ID, sampled.Ahead.ID);
        Assert.Equal(iterated.Behind.ID, sampled.Behind.ID);
    }

    [Fact]
    public void SamplerRotatesNeighboursByVoxelOrientation()
    {
        var voxels = new VoxelObject(4);
        var yaw = new Orientation(pitch: 0, yaw: 1, roll: 0);
        voxels.Set(1, 1, 1, new Voxel(1, 0, yaw));
        voxels.Set(2, 1, 1, new Voxel(2, 0, 0));
        voxels.Set(1, 1, 2, new Voxel(3, 0, 0));

        VoxelSample sample = SampleAt(voxels, 1, 1, 1);

        //  A quarter yaw moves Ahead (+Z) onto +X and Left (-X) onto +Z.
        Assert.Equal(2, sample.Ahead.ID);
        Assert.Equal(3, sample.Left.ID);
    }

    [Fact]
    public void SamplerAcceptsAnOutOfRangeOrientation()
    {
        var voxels = new VoxelObject(4, [
            new ChunkInfo(0, 0, 0, new Chunk(4, SampleTestIndex.Fill(chunkSize: 4, (1, 1, 1, new Voxel(1, 0, 200))))),
        ]);

        VoxelSample sample = SampleAt(voxels, 1, 1, 1);

        Assert.Equal(1, sample.Center.ID);
    }

    [Fact]
    public void ConstructorRejectsAChunkWithTheWrongSize()
    {
        ChunkInfo[] chunks = [new(0, 0, 0, new Chunk(8, new Voxel[512]))];

        Assert.Throws<ArgumentException>(() => new VoxelObject(chunkSize: 16, chunks));
    }

    [Fact]
    public void ConstructorRejectsAChunkWithTheWrongVoxelCount()
    {
        ChunkInfo[] chunks = [new(0, 0, 0, new Chunk(4, new Voxel[10]))];

        Assert.Throws<ArgumentException>(() => new VoxelObject(chunkSize: 4, chunks));
    }

    [Fact]
    public void ConstructorRejectsAChunkWithoutVoxels()
    {
        ChunkInfo[] chunks = [new(0, 0, 0, new Chunk(4, null!))];

        Assert.Throws<ArgumentException>(() => new VoxelObject(chunkSize: 4, chunks));
    }

    [Fact]
    public void ConstructorCountsEveryVoxelIntoThePalette()
    {
        var voxels = new VoxelObject(4, [
            new ChunkInfo(0, 0, 0, new Chunk(4, SampleTestIndex.Fill(chunkSize: 4, (0, 0, 0, new Voxel(3, 0, 0))))),
        ]);

        ChunkData chunk = Single(voxels);

        Assert.Equal(1, chunk.Palette.Count(id: 3));
        Assert.Equal(63, chunk.Palette.Count(id: 0));
    }

    private static VoxelSample SampleAt(VoxelObject voxels, int x, int y, int z)
    {
        foreach (ChunkData chunk in voxels)
        foreach (VoxelSample sample in chunk.GetSampler())
        {
            if (sample.Coords.X == x && sample.Coords.Y == y && sample.Coords.Z == z)
            {
                return sample;
            }
        }

        throw new InvalidOperationException($"No sample at {x},{y},{z}.");
    }

    private static ChunkData Single(VoxelObject voxels)
    {
        foreach (ChunkData chunk in voxels)
        {
            return chunk;
        }

        throw new InvalidOperationException("The container has no chunk.");
    }
}

/// <summary>Builds chunk content for tests.</summary>
internal static class SampleTestIndex
{
    public static int LocalIndex(int x, int y, int z, int chunkSize)
    {
        int shift = BitOperations.TrailingZeroCount(chunkSize);
        return x + (y << shift) + (z << (shift * 2));
    }

    public static Voxel[] Fill(int chunkSize, params (int X, int Y, int Z, Voxel Voxel)[] entries)
    {
        var voxels = new Voxel[chunkSize * chunkSize * chunkSize];
        foreach ((int x, int y, int z, Voxel voxel) in entries)
        {
            voxels[LocalIndex(x, y, z, chunkSize)] = voxel;
        }

        return voxels;
    }
}

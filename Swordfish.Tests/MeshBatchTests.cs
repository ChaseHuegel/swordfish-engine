using System;
using System.Numerics;
using Swordfish.Graphics;
using Xunit;

namespace Swordfish.Tests;

public class MeshBatchTests
{
    private static float[] Vertices(int count)
    {
        var data = new float[count * MeshBatch.VERTEX_STRIDE];
        for (var i = 0; i < data.Length; i++)
        {
            data[i] = i;
        }

        return data;
    }

    private static uint[] Indices(int count)
    {
        var data = new uint[count];
        for (var i = 0; i < data.Length; i++)
        {
            data[i] = (uint)i;
        }

        return data;
    }

    [Fact]
    public void AddUpdateRemoveAndStaleHandles()
    {
        var batch = new MeshBatch();
        MeshRangeHandle handle = batch.AddRange(Vertices(3), 3, Indices(6), new BoundingBox(Vector3.Zero, Vector3.One, 1f));

        Assert.True(handle.IsValid);
        Assert.Equal(1, batch.RangeCount);
        Assert.True(batch.UpdateRange(handle, Vertices(4), 4, Indices(6), new BoundingBox(Vector3.Zero, Vector3.One, 1f)));
        Assert.True(batch.RemoveRange(handle));
        Assert.False(batch.UpdateRange(handle, Vertices(3), 3, Indices(3), default));
        Assert.Equal(0, batch.RangeCount);
    }

    [Fact]
    public void GrowthMovesTheRangeAndBumpsTheVersion()
    {
        var batch = new MeshBatch();
        MeshRangeHandle handle = batch.AddRange(Vertices(2), 2, Indices(3), default);
        MeshRange before = batch.Ranges[handle.Slot];

        Assert.True(batch.UpdateRange(handle, Vertices(2048), 2048, Indices(4096), default));

        MeshRange after = batch.Ranges[handle.Slot];
        Assert.NotEqual(before.VertexOffset, after.VertexOffset);
        Assert.True(after.Version > before.Version);
        Assert.Equal(2048, after.VertexCount);
    }

    [Fact]
    public void RemovingRangesCompactsTheArena()
    {
        var batch = new MeshBatch();
        MeshRangeHandle keep = batch.AddRange(Vertices(4), 4, Indices(6), default);
        MeshRangeHandle drop = batch.AddRange(Vertices(4), 4, Indices(6), default);
        int generation = batch.Generation;

        Assert.True(batch.RemoveRange(drop));
        Assert.True(batch.UpdateRange(keep, Vertices(4), 4, Indices(6), default));

        MeshRange range = batch.Ranges[keep.Slot];
        Assert.Equal(0, range.VertexOffset);
        Assert.True(batch.Generation > generation);
    }

    [Fact]
    public void WriterCommitsAValidRange()
    {
        var batch = new MeshBatch();
        var writer = new MeshBatchWriter();
        writer.BeginRange();
        writer.AddVertex(Vector3.Zero, Vector4.One, Vector3.Zero, Vector3.UnitY);
        writer.AddVertex(Vector3.UnitX, Vector4.One, Vector3.Zero, Vector3.UnitY);
        writer.AddVertex(Vector3.UnitY, Vector4.One, Vector3.Zero, Vector3.UnitY);
        writer.AddTriangle(0, 1, 2);

        MeshRangeHandle handle = writer.Commit(batch, default);

        Assert.True(handle.IsValid);
        Assert.Equal(1, batch.RangeCount);
        Assert.Equal(3, batch.Ranges[handle.Slot].VertexCount);
        Assert.Equal(3, batch.Ranges[handle.Slot].IndexCount);
    }

    [Fact]
    public void ReusedSlotRejectsTheOldHandle()
    {
        var batch = new MeshBatch();
        MeshRangeHandle first = batch.AddRange(Vertices(2), 2, Indices(3), default);
        Assert.True(batch.RemoveRange(first));

        MeshRangeHandle second = batch.AddRange(Vertices(2), 2, Indices(3), default);
        Assert.Equal(first.Slot, second.Slot);
        Assert.NotEqual(first.Generation, second.Generation);
        Assert.False(batch.RemoveRange(first));
        Assert.True(batch.RemoveRange(second));
    }
}

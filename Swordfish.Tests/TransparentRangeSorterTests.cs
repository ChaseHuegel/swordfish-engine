using System;
using System.Collections.Generic;
using System.Numerics;
using Swordfish.Graphics;
using Swordfish.Settings;
using Xunit;

namespace Swordfish.Tests;

public class TransparentRangeSorterTests
{
    private static void AddQuad(List<float> vertices, List<uint> indices, float z)
    {
        var start = (uint)(vertices.Count / MeshBatch.VERTEX_STRIDE);
        AddVertex(vertices, new Vector3(-0.5f, -0.5f, z));
        AddVertex(vertices, new Vector3(0.5f, -0.5f, z));
        AddVertex(vertices, new Vector3(0.5f, 0.5f, z));
        AddVertex(vertices, new Vector3(-0.5f, 0.5f, z));

        indices.Add(start + 0);
        indices.Add(start + 1);
        indices.Add(start + 2);
        indices.Add(start + 0);
        indices.Add(start + 2);
        indices.Add(start + 3);
    }

    private static void AddTriangle(List<float> vertices, List<uint> indices, float z)
    {
        var start = (uint)(vertices.Count / MeshBatch.VERTEX_STRIDE);
        AddVertex(vertices, new Vector3(-0.5f, -0.5f, z));
        AddVertex(vertices, new Vector3(0.5f, -0.5f, z));
        AddVertex(vertices, new Vector3(0f, 0.5f, z));

        indices.Add(start + 0);
        indices.Add(start + 1);
        indices.Add(start + 2);
    }

    private static void AddVertex(List<float> vertices, Vector3 position)
    {
        vertices.Add(position.X);
        vertices.Add(position.Y);
        vertices.Add(position.Z);
        for (var i = 3; i < MeshBatch.VERTEX_STRIDE; i++)
        {
            vertices.Add(0f);
        }
    }

    [Fact]
    public void SortsQuadsBackToFrontAndReversesFromOppositeSide()
    {
        var batch = new MeshBatch();
        var vertices = new List<float>();
        var indices = new List<uint>();

        //  Quad 0 at z = 1 (vertices 0..3), Quad 1 at z = 5 (vertices 4..7), Quad 2 at z = 3 (vertices 8..11)
        AddQuad(vertices, indices, 1f);
        AddQuad(vertices, indices, 5f);
        AddQuad(vertices, indices, 3f);

        MeshRangeHandle handle = batch.AddRange(vertices.ToArray(), vertices.Count / MeshBatch.VERTEX_STRIDE, indices.ToArray(), default);
        MeshRange range = batch.Ranges[handle.Slot];
        var sorter = new TransparentRangeSorter();

        //  Camera at z = 0: farthest to nearest is Quad 1 (z=5), Quad 2 (z=3), Quad 0 (z=1)
        uint[] fromFront = sorter.SortIfNeeded(handle.Slot, in range, new Vector3(0f, 0f, 0f), 0.25f).ToArray();
        Assert.Equal(
            [4u, 5u, 6u, 4u, 6u, 7u, 8u, 9u, 10u, 8u, 10u, 11u, 0u, 1u, 2u, 0u, 2u, 3u],
            fromFront
        );

        //  Camera at z = 10: farthest to nearest is Quad 0 (z=1), Quad 2 (z=3), Quad 1 (z=5)
        uint[] fromBack = sorter.SortIfNeeded(handle.Slot, in range, new Vector3(0f, 0f, 10f), 0.25f).ToArray();
        Assert.Equal(
            [0u, 1u, 2u, 0u, 2u, 3u, 8u, 9u, 10u, 8u, 10u, 11u, 4u, 5u, 6u, 4u, 6u, 7u],
            fromBack
        );
    }

    [Fact]
    public void SortsMixedQuadsAndStandaloneTriangles()
    {
        var batch = new MeshBatch();
        var vertices = new List<float>();
        var indices = new List<uint>();

        //  Quad at z = 2 (vertices 0..3), standalone triangle at z = 6 (vertices 4..6), quad at z = 4 (vertices 7..10)
        AddQuad(vertices, indices, 2f);
        AddTriangle(vertices, indices, 6f);
        AddQuad(vertices, indices, 4f);

        MeshRangeHandle handle = batch.AddRange(vertices.ToArray(), vertices.Count / MeshBatch.VERTEX_STRIDE, indices.ToArray(), default);
        MeshRange range = batch.Ranges[handle.Slot];
        var sorter = new TransparentRangeSorter();

        uint[] sorted = sorter.SortIfNeeded(handle.Slot, in range, new Vector3(0f, 0f, 0f), 0.25f).ToArray();
        Assert.Equal(
            [4u, 5u, 6u, 7u, 8u, 9u, 7u, 9u, 10u, 0u, 1u, 2u, 0u, 2u, 3u],
            sorted
        );
    }

    [Fact]
    public void MovementThresholdSkipsRedundantSortsUntilExceeded()
    {
        var batch = new MeshBatch();
        var vertices = new List<float>();
        var indices = new List<uint>();
        AddQuad(vertices, indices, 1f);
        AddQuad(vertices, indices, 4f);

        MeshRangeHandle handle = batch.AddRange(vertices.ToArray(), vertices.Count / MeshBatch.VERTEX_STRIDE, indices.ToArray(), default);
        MeshRange range = batch.Ranges[handle.Slot];
        var sorter = new TransparentRangeSorter();

        //  Initial sort always runs
        Assert.False(sorter.SortIfNeeded(handle.Slot, in range, Vector3.Zero, 0.25f).IsEmpty);

        //  Movement below 0.25f does not re-sort
        Assert.True(sorter.SortIfNeeded(handle.Slot, in range, new Vector3(0.24f, 0f, 0f), 0.25f).IsEmpty);

        //  Movement at or above 0.25f triggers a re-sort
        Assert.False(sorter.SortIfNeeded(handle.Slot, in range, new Vector3(0.25f, 0f, 0f), 0.25f).IsEmpty);

        //  Stationary camera with zero threshold does not re-sort
        Assert.True(sorter.SortIfNeeded(handle.Slot, in range, new Vector3(0.25f, 0f, 0f), 0f).IsEmpty);
        Assert.False(sorter.SortIfNeeded(handle.Slot, in range, new Vector3(0.26f, 0f, 0f), 0f).IsEmpty);
    }

    [Fact]
    public void InvalidationAndRangeUpdatesForceResort()
    {
        var batch = new MeshBatch();
        var vertices = new List<float>();
        var indices = new List<uint>();
        AddQuad(vertices, indices, 1f);
        AddQuad(vertices, indices, 4f);

        MeshRangeHandle handle = batch.AddRange(vertices.ToArray(), vertices.Count / MeshBatch.VERTEX_STRIDE, indices.ToArray(), default);
        MeshRange range = batch.Ranges[handle.Slot];
        var sorter = new TransparentRangeSorter();

        Assert.False(sorter.SortIfNeeded(handle.Slot, in range, Vector3.Zero, 0.25f).IsEmpty);
        Assert.True(sorter.SortIfNeeded(handle.Slot, in range, Vector3.Zero, 0.25f).IsEmpty);

        //  Arena resize / compaction invalidates GPU order without camera movement
        sorter.InvalidateUploadedOrder();
        Assert.False(sorter.SortIfNeeded(handle.Slot, in range, Vector3.Zero, 0.25f).IsEmpty);
        Assert.True(sorter.SortIfNeeded(handle.Slot, in range, Vector3.Zero, 0.25f).IsEmpty);

        //  Updating the range bumps Version and rebuilds centroids
        vertices.Clear();
        indices.Clear();
        AddQuad(vertices, indices, 10f);
        AddQuad(vertices, indices, 2f);
        Assert.True(batch.UpdateRange(handle, vertices.ToArray(), vertices.Count / MeshBatch.VERTEX_STRIDE, indices.ToArray(), default));

        MeshRange updated = batch.Ranges[handle.Slot];
        uint[] resorted = sorter.SortIfNeeded(handle.Slot, in updated, Vector3.Zero, 0.25f).ToArray();
        Assert.Equal([0u, 1u, 2u, 0u, 2u, 3u, 4u, 5u, 6u, 4u, 6u, 7u], resorted);
    }

    [Fact]
    public void ReusedSlotRebuildsPrimitives()
    {
        var batch = new MeshBatch();
        var vertices = new List<float>();
        var indices = new List<uint>();
        AddQuad(vertices, indices, 1f);
        AddQuad(vertices, indices, 5f);

        MeshRangeHandle first = batch.AddRange(vertices.ToArray(), vertices.Count / MeshBatch.VERTEX_STRIDE, indices.ToArray(), default);
        var sorter = new TransparentRangeSorter();
        Assert.False(sorter.SortIfNeeded(first.Slot, batch.Ranges[first.Slot], Vector3.Zero, 0.25f).IsEmpty);

        Assert.True(batch.RemoveRange(first));
        sorter.ClearInactiveSlot(first.Slot);

        vertices.Clear();
        indices.Clear();
        AddQuad(vertices, indices, 8f);
        AddQuad(vertices, indices, 2f);
        MeshRangeHandle second = batch.AddRange(vertices.ToArray(), vertices.Count / MeshBatch.VERTEX_STRIDE, indices.ToArray(), default);

        Assert.Equal(first.Slot, second.Slot);
        uint[] sorted = sorter.SortIfNeeded(second.Slot, batch.Ranges[second.Slot], Vector3.Zero, 0.25f).ToArray();
        Assert.Equal([0u, 1u, 2u, 0u, 2u, 3u, 4u, 5u, 6u, 4u, 6u, 7u], sorted);
    }

    [Fact]
    public void SingleQuadRangeSkipsUpload()
    {
        var batch = new MeshBatch();
        var vertices = new List<float>();
        var indices = new List<uint>();
        AddQuad(vertices, indices, 2f);

        MeshRangeHandle handle = batch.AddRange(vertices.ToArray(), vertices.Count / MeshBatch.VERTEX_STRIDE, indices.ToArray(), default);
        var sorter = new TransparentRangeSorter();

        Assert.True(sorter.SortIfNeeded(handle.Slot, batch.Ranges[handle.Slot], Vector3.Zero, 0.25f).IsEmpty);
    }

    [Fact]
    public void LocalSpaceCameraTransformHandlesRotatedBatch()
    {
        var batch = new MeshBatch();
        var vertices = new List<float>();
        var indices = new List<uint>();

        //  Quad 0 at local +Z (z=1), Quad 1 at local +Z (z=5)
        AddQuad(vertices, indices, 1f);
        AddQuad(vertices, indices, 5f);

        MeshRangeHandle handle = batch.AddRange(vertices.ToArray(), vertices.Count / MeshBatch.VERTEX_STRIDE, indices.ToArray(), default);
        MeshRange range = batch.Ranges[handle.Slot];
        var sorter = new TransparentRangeSorter();

        //  Rotate batch 180 degrees around Y so local +Z points along world -Z
        Matrix4x4 model = Matrix4x4.CreateRotationY(MathF.PI);
        Assert.True(Matrix4x4.Invert(model, out Matrix4x4 invModel));

        //  World camera at (0, 0, -10) maps to local (0, 0, +10), so Quad 0 (z=1) is farther than Quad 1 (z=5)
        Vector3 localCameraPos = Vector3.Transform(new Vector3(0f, 0f, -10f), invModel);
        uint[] sorted = sorter.SortIfNeeded(handle.Slot, in range, in localCameraPos, 0.25f).ToArray();
        Assert.Equal([0u, 1u, 2u, 0u, 2u, 3u, 4u, 5u, 6u, 4u, 6u, 7u], sorted);
    }

    [Fact]
    public void RenderSettingsDefaultTransparentSortThresholdIsQuarterUnit()
    {
        var settings = new RenderSettings();
        Assert.Equal(0.25f, settings.TransparentSortThreshold.Get());
    }

    [Fact]
    public void NonCoplanarSharedEdgeTrianglesSortIndependently()
    {
        var batch = new MeshBatch();
        var vertices = new List<float>();
        var indices = new List<uint>();

        //  Two orthogonal triangles sharing edge (0, 2): tri 0 lies in z = 0, tri 1 folds to z = 2
        AddVertex(vertices, new Vector3(0f, 0f, 0f));
        AddVertex(vertices, new Vector3(1f, 0f, 0f));
        AddVertex(vertices, new Vector3(0f, 1f, 0f));
        AddVertex(vertices, new Vector3(0f, 0f, 2f));

        indices.Add(0);
        indices.Add(1);
        indices.Add(2);
        indices.Add(0);
        indices.Add(2);
        indices.Add(3);

        MeshRangeHandle handle = batch.AddRange(vertices.ToArray(), vertices.Count / MeshBatch.VERTEX_STRIDE, indices.ToArray(), default);
        MeshRange range = batch.Ranges[handle.Slot];
        var sorter = new TransparentRangeSorter();

        //  Camera at z = -5: tri 1 (centroid z = 2/3) is farther than tri 0 (centroid z = 0)
        uint[] sorted = sorter.SortIfNeeded(handle.Slot, in range, new Vector3(0f, 0f, -5f), 0.25f).ToArray();
        Assert.Equal([0u, 2u, 3u, 0u, 1u, 2u], sorted);
    }
}

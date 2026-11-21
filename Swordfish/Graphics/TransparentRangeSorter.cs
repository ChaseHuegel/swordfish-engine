using System.Numerics;

namespace Swordfish.Graphics;

/// <summary>
/// Sorts transparent <see cref="MeshRange"/> quad and triangle indices back-to-front relative to a local camera position.
/// </summary>
internal sealed class TransparentRangeSorter
{
    private RangeSortState[] _states = [];
    private float[] _sortKeys = [];
    private int[] _sortOrder = [];
    private uint[] _sortedIndices = [];

    /// <summary>Marks every range as needing an index re-upload while keeping cached primitive centroids.</summary>
    public void InvalidateUploadedOrder()
    {
        for (var i = 0; i < _states.Length; i++)
        {
            _states[i].HasSorted = false;
        }
    }

    /// <summary>Marks one range as needing an index re-upload.</summary>
    public void InvalidateRange(int slot)
    {
        if (slot >= 0 && slot < _states.Length)
        {
            _states[slot].HasSorted = false;
        }
    }

    /// <summary>Releases cached primitive data for an inactive range slot.</summary>
    public void ClearInactiveSlot(int slot)
    {
        if (slot >= 0 && slot < _states.Length && _states[slot].Primitives != null)
        {
            _states[slot] = default;
        }
    }

    /// <summary>Returns sorted indices when the range must be re-uploaded, or an empty span when the current GPU order is still valid.</summary>
    public ReadOnlySpan<uint> SortIfNeeded(int slot, in MeshRange range, in Vector3 localCameraPos, float movementThreshold)
    {
        if (slot < 0 || !range.Active || range.IndexCount < 3 || range.Indices == null || range.VertexData == null)
        {
            return ReadOnlySpan<uint>.Empty;
        }

        if (slot >= _states.Length)
        {
            Array.Resize(ref _states, Math.Max(slot + 1, _states.Length * 2));
        }

        ref RangeSortState state = ref _states[slot];
        if (state.Primitives == null || state.RangeGeneration != range.Generation || state.RangeVersion != range.Version)
        {
            state.Primitives = BuildPrimitives(range.VertexData, range.VertexCount, range.Indices, range.IndexCount);
            state.RangeGeneration = range.Generation;
            state.RangeVersion = range.Version;
            state.HasSorted = false;
        }

        SortPrimitive[] primitives = state.Primitives;
        if (primitives.Length <= 1)
        {
            state.LastSortCameraLocalPos = localCameraPos;
            state.HasSorted = true;
            return ReadOnlySpan<uint>.Empty;
        }

        if (state.HasSorted)
        {
            float movedSq = Vector3.DistanceSquared(localCameraPos, state.LastSortCameraLocalPos);
            float threshold = float.IsFinite(movementThreshold) && movementThreshold > 0f ? movementThreshold : 0f;
            bool exceeded = threshold > 0f ? movedSq >= threshold * threshold : movedSq > 0f;
            if (!exceeded)
            {
                return ReadOnlySpan<uint>.Empty;
            }
        }

        EnsureScratchCapacity(primitives.Length, range.IndexCount);

        for (var i = 0; i < primitives.Length; i++)
        {
            _sortKeys[i] = -Vector3.DistanceSquared(primitives[i].Centroid, localCameraPos);
            _sortOrder[i] = i;
        }

        Array.Sort(_sortKeys, _sortOrder, 0, primitives.Length);

        uint[] sourceIndices = range.Indices;
        var writePos = 0;
        for (var i = 0; i < primitives.Length; i++)
        {
            SortPrimitive primitive = primitives[_sortOrder[i]];
            int src = primitive.SourceIndexOffset;
            if (primitive.IndexCount == 6)
            {
                _sortedIndices[writePos] = sourceIndices[src];
                _sortedIndices[writePos + 1] = sourceIndices[src + 1];
                _sortedIndices[writePos + 2] = sourceIndices[src + 2];
                _sortedIndices[writePos + 3] = sourceIndices[src + 3];
                _sortedIndices[writePos + 4] = sourceIndices[src + 4];
                _sortedIndices[writePos + 5] = sourceIndices[src + 5];
                writePos += 6;
            }
            else
            {
                _sortedIndices[writePos] = sourceIndices[src];
                _sortedIndices[writePos + 1] = sourceIndices[src + 1];
                _sortedIndices[writePos + 2] = sourceIndices[src + 2];
                writePos += 3;
            }
        }

        state.LastSortCameraLocalPos = localCameraPos;
        state.HasSorted = true;
        return _sortedIndices.AsSpan(0, writePos);
    }

    private void EnsureScratchCapacity(int primitiveCount, int indexCount)
    {
        if (_sortKeys.Length < primitiveCount)
        {
            int next = Math.Max(primitiveCount, _sortKeys.Length * 2);
            _sortKeys = new float[next];
            _sortOrder = new int[next];
        }

        if (_sortedIndices.Length < indexCount)
        {
            _sortedIndices = new uint[Math.Max(indexCount, _sortedIndices.Length * 2)];
        }
    }

    private static SortPrimitive[] BuildPrimitives(float[] vertexData, int vertexCount, uint[] indices, int indexCount)
    {
        int maxPrimitives = indexCount / 3;
        int maxVertex = Math.Min(vertexCount, vertexData.Length / MeshBatch.VERTEX_STRIDE);
        if (maxPrimitives == 0 || maxVertex <= 0)
        {
            return [];
        }

        var primitives = new List<SortPrimitive>(maxPrimitives);
        var i = 0;
        while (i + 2 < indexCount)
        {
            uint i0 = indices[i];
            uint i1 = indices[i + 1];
            uint i2 = indices[i + 2];

            Vector3 p0 = ReadPosition(vertexData, maxVertex, i0);
            Vector3 p1 = ReadPosition(vertexData, maxVertex, i1);
            Vector3 p2 = ReadPosition(vertexData, maxVertex, i2);

            if (i + 5 < indexCount && indices[i + 3] == i0 && indices[i + 4] == i2)
            {
                Vector3 p3 = ReadPosition(vertexData, maxVertex, indices[i + 5]);
                if (AreCoplanarSameWinding(p0, p1, p2, p3))
                {
                    primitives.Add(new SortPrimitive((p0 + p1 + p2 + p3) * 0.25f, i, 6));
                    i += 6;
                    continue;
                }
            }

            primitives.Add(new SortPrimitive((p0 + p1 + p2) * (1f / 3f), i, 3));
            i += 3;
        }

        return primitives.ToArray();
    }

    private static bool AreCoplanarSameWinding(in Vector3 p0, in Vector3 p1, in Vector3 p2, in Vector3 p3)
    {
        Vector3 nA = Vector3.Cross(p1 - p0, p2 - p0);
        Vector3 nB = Vector3.Cross(p2 - p0, p3 - p0);
        float dot = Vector3.Dot(nA, nB);
        return dot > 0f && dot * dot >= 0.999f * nA.LengthSquared() * nB.LengthSquared();
    }

    private static Vector3 ReadPosition(float[] vertexData, int maxVertex, uint vertexIndex)
    {
        if (vertexIndex >= (uint)maxVertex)
        {
            return Vector3.Zero;
        }

        int offset = (int)vertexIndex * MeshBatch.VERTEX_STRIDE;
        return new Vector3(vertexData[offset], vertexData[offset + 1], vertexData[offset + 2]);
    }

    private readonly struct SortPrimitive(Vector3 centroid, int sourceIndexOffset, byte indexCount)
    {
        public readonly Vector3 Centroid = centroid;
        public readonly int SourceIndexOffset = sourceIndexOffset;
        public readonly byte IndexCount = indexCount;
    }

    private struct RangeSortState
    {
        public int RangeGeneration;
        public uint RangeVersion;
        public Vector3 LastSortCameraLocalPos;
        public bool HasSorted;
        public SortPrimitive[]? Primitives;
    }
}

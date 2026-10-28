using System.Numerics;
using System.Runtime.InteropServices;

namespace Swordfish.Graphics;

/// <summary>
/// A pooled CPU builder for one <see cref="MeshBatch"/> range.
/// </summary>
public sealed class MeshBatchWriter
{
    private readonly List<float> _vertices = [];
    private readonly List<uint> _indices = [];

    /// <summary>Starts a new range and clears the buffered data.</summary>
    public void BeginRange()
    {
        _vertices.Clear();
        _indices.Clear();
    }

    /// <summary>Appends one vertex in the batch vertex layout.</summary>
    public void AddVertex(in Vector3 position, in Vector4 color, in Vector3 uv, in Vector3 normal)
    {
        _vertices.Add(position.X);
        _vertices.Add(position.Y);
        _vertices.Add(position.Z);
        _vertices.Add(color.X);
        _vertices.Add(color.Y);
        _vertices.Add(color.Z);
        _vertices.Add(color.W);
        _vertices.Add(uv.X);
        _vertices.Add(uv.Y);
        _vertices.Add(uv.Z);
        _vertices.Add(normal.X);
        _vertices.Add(normal.Y);
        _vertices.Add(normal.Z);
    }

    /// <summary>Appends one triangle. Indices are local to the current range.</summary>
    public void AddTriangle(uint a, uint b, uint c)
    {
        _indices.Add(a);
        _indices.Add(b);
        _indices.Add(c);
    }

    /// <summary>Commits the buffered range to a batch.</summary>
    public MeshRangeHandle Commit(MeshBatch batch, in BoundingBox bounds)
    {
        if (_vertices.Count == 0 || _indices.Count == 0)
        {
            return default;
        }

        return batch.AddRange(
            vertexData: CollectionsMarshal.AsSpan(_vertices),
            vertexCount: _vertices.Count / MeshBatch.VERTEX_STRIDE,
            indices: CollectionsMarshal.AsSpan(_indices),
            bounds: bounds
        );
    }
}

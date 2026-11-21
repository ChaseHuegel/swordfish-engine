using System.Numerics;

namespace Swordfish.Graphics.Jolt;

/// <summary>
///     Accumulates Jolt debug lines and triangles into flat reusable vertex buffers.
/// </summary>
internal sealed class JoltDebugBatch
{
    private const int INITIAL_CAPACITY = 4096;

    private JoltDebugVertex[] _lineVertices = [];
    private int _lineVertexCount;

    private JoltDebugVertex[] _triangleVertices = [];
    private int _triangleVertexCount;

    /// <summary>Number of line vertices currently queued.</summary>
    public int LineVertexCount => _lineVertexCount;

    /// <summary>Number of triangle vertices currently queued.</summary>
    public int TriangleVertexCount => _triangleVertexCount;

    /// <summary>True when neither lines nor triangles are queued.</summary>
    public bool IsEmpty => _lineVertexCount == 0 && _triangleVertexCount == 0;

    /// <summary>Contiguous span of queued line vertices.</summary>
    public ReadOnlySpan<JoltDebugVertex> LineVertices => _lineVertices.AsSpan(0, _lineVertexCount);

    /// <summary>Contiguous span of queued triangle vertices.</summary>
    public ReadOnlySpan<JoltDebugVertex> TriangleVertices => _triangleVertices.AsSpan(0, _triangleVertexCount);

    /// <summary>Resets queued counts without releasing backing arrays.</summary>
    public void Clear()
    {
        _lineVertexCount = 0;
        _triangleVertexCount = 0;
    }

    /// <summary>Appends a line segment to the line vertex buffer.</summary>
    public void AddLine(in Vector3 from, in Vector3 to, uint packedColor)
    {
        EnsureCapacity(ref _lineVertices, _lineVertexCount + 2);
        _lineVertices[_lineVertexCount++] = new JoltDebugVertex(from, packedColor);
        _lineVertices[_lineVertexCount++] = new JoltDebugVertex(to, packedColor);
    }

    /// <summary>Appends a triangle to the triangle vertex buffer.</summary>
    public void AddTriangle(in Vector3 v1, in Vector3 v2, in Vector3 v3, uint packedColor)
    {
        EnsureCapacity(ref _triangleVertices, _triangleVertexCount + 3);
        _triangleVertices[_triangleVertexCount++] = new JoltDebugVertex(v1, packedColor);
        _triangleVertices[_triangleVertexCount++] = new JoltDebugVertex(v2, packedColor);
        _triangleVertices[_triangleVertexCount++] = new JoltDebugVertex(v3, packedColor);
    }

    private static void EnsureCapacity(ref JoltDebugVertex[] buffer, int required)
    {
        if (required <= buffer.Length)
        {
            return;
        }

        int nextCapacity = buffer.Length == 0 ? INITIAL_CAPACITY : buffer.Length * 2;
        while (nextCapacity < required)
        {
            nextCapacity *= 2;
        }

        Array.Resize(ref buffer, nextCapacity);
    }
}

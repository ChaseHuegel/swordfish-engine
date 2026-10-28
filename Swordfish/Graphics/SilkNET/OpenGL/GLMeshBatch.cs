using System.Numerics;
using Silk.NET.OpenGL;

namespace Swordfish.Graphics.SilkNET.OpenGL;

/// <summary>
/// Owns the OpenGL buffers for one <see cref="MeshBatch"/>.
/// </summary>
internal sealed unsafe class GLMeshBatch : IDisposable
{
    private readonly GL _gl;
    private readonly GLContext _glContext;
    private readonly MeshBatch _batch;
    private readonly BufferObject<Matrix4x4> _modelBuffer;
    private readonly Matrix4x4[] _modelMatrix = new Matrix4x4[1];
    private readonly List<Arena> _arenas = [];

    private uint[] _lastVersions = [];
    private int _lastGeneration = -1;
    private Matrix4x4 _lastModelMatrix;
    private bool _hasModelMatrix;

    public GLMeshBatch(in GL gl, in GLContext glContext, in MeshBatch batch, in GLMaterial[] materials)
    {
        _gl = gl;
        _glContext = glContext;
        _batch = batch;
        Materials = materials;
        _modelBuffer = _glContext.CreateBufferObject(_modelMatrix, BufferTargetARB.ArrayBuffer, BufferUsageARB.DynamicDraw);
    }

    public MeshBatch Batch => _batch;

    public GLMaterial[] Materials { get; }

    /// <summary>Uploads changed ranges and the model matrix. Call on the GL thread before any draw.</summary>
    public void Upload(in Matrix4x4 modelMatrix)
    {
        if (!_hasModelMatrix || _lastModelMatrix != modelMatrix)
        {
            _modelMatrix[0] = modelMatrix;
            _modelBuffer.UpdateData(_modelMatrix);
            _lastModelMatrix = modelMatrix;
            _hasModelMatrix = true;
        }

        lock (_batch.SyncRoot)
        {
            if (_batch.Generation != _lastGeneration || _arenas.Count != _batch.Arenas.Count)
            {
                SyncArenas();
                Array.Clear(_lastVersions);
                _lastGeneration = _batch.Generation;
            }

            if (_lastVersions.Length < _batch.Ranges.Count)
            {
                Array.Resize(ref _lastVersions, _batch.Ranges.Count);
            }

            EnsureCapacities();

            for (var slot = 0; slot < _batch.Ranges.Count; slot++)
            {
                MeshRange range = _batch.Ranges[slot];
                if (!range.Active || range.Arena >= _arenas.Count || range.Version == _lastVersions[slot])
                {
                    continue;
                }

                Arena arena = _arenas[range.Arena];
                arena.VertexBuffer.UpdateRangeData(
                    range.VertexOffset * MeshBatch.VERTEX_STRIDE * sizeof(float),
                    range.VertexData.AsSpan(0, range.VertexCount * MeshBatch.VERTEX_STRIDE)
                );
                //  Bind the owning VAO first. BufferObject unbinds the element buffer,
                //  and an unbind while a VAO is bound clears that VAO's index binding.
                arena.VertexArrayObject.Bind();
                arena.IndexBuffer.UpdateRangeData(
                    range.IndexOffset * sizeof(uint),
                    range.Indices.AsSpan(0, range.IndexCount)
                );
                arena.IndexBuffer.Bind();
                _lastVersions[slot] = range.Version;
            }
        }
    }

    /// <summary>Draws one arena with a compacted set of ranges.</summary>
    public void Draw(int arenaIndex, ReadOnlySpan<uint> counts, ReadOnlySpan<nint> indexOffsets, ReadOnlySpan<int> baseVertices)
    {
        if (arenaIndex < 0 || arenaIndex >= _arenas.Count || counts.Length == 0)
        {
            return;
        }

        Arena arena = _arenas[arenaIndex];
        arena.VertexArrayObject.Bind();

        //  Another renderer can unbind an element buffer while this VAO is current.
        //  That clears the VAO index binding, so restore it before every draw.
        arena.IndexBuffer.Bind();

        fixed (nint* offsets = indexOffsets)
        {
            _gl.MultiDrawElementsBaseVertex(
                PrimitiveType.Triangles,
                counts,
                DrawElementsType.UnsignedInt,
                (void**)offsets,
                baseVertices
            );
        }
    }

    private void SyncArenas()
    {
        for (var i = 0; i < _batch.Arenas.Count; i++)
        {
            if (i >= _arenas.Count)
            {
                _arenas.Add(CreateArena(_batch.Arenas[i]));
            }
        }

        for (var i = _arenas.Count - 1; i >= _batch.Arenas.Count; i--)
        {
            _arenas[i].Dispose();
            _arenas.RemoveAt(i);
        }
    }

    private void EnsureCapacities()
    {
        var resized = false;
        for (var arenaIndex = 0; arenaIndex < _arenas.Count; arenaIndex++)
        {
            var maxVertices = 0;
            var maxIndices = 0;
            for (var slot = 0; slot < _batch.Ranges.Count; slot++)
            {
                MeshRange range = _batch.Ranges[slot];
                if (!range.Active || range.Arena != arenaIndex)
                {
                    continue;
                }

                maxVertices = Math.Max(maxVertices, range.VertexOffset + range.VertexCount);
                maxIndices = Math.Max(maxIndices, range.IndexOffset + range.IndexCount);
            }

            Arena arena = _arenas[arenaIndex];
            if (maxVertices > arena.VertexCapacity || maxIndices > arena.IndexCapacity)
            {
                arena.Resize(
                    Math.Max(maxVertices, arena.VertexCapacity * 2),
                    Math.Max(maxIndices, arena.IndexCapacity * 2)
                );
                resized = true;
            }
        }

        if (resized)
        {
            Array.Clear(_lastVersions);
        }
    }

    private Arena CreateArena(in MeshBatchArena source)
    {
        VertexArrayObject32 vao = _glContext.CreateVertexArrayObject32([], []);
        vao.Bind();

        int vertexCapacity = Math.Max(4096, source.UsedVertices + (source.UsedVertices / 4));
        int indexCapacity = Math.Max(8192, source.UsedIndices + (source.UsedIndices / 4));
        var arena = new Arena(vao);
        arena.Resize(vertexCapacity, indexCapacity);

        vao.VertexBufferObject.Bind();
        vao.SetVertexAttribute(0, 3, VertexAttribPointerType.Float, MeshBatch.VERTEX_STRIDE, 0);
        vao.SetVertexAttribute(1, 4, VertexAttribPointerType.Float, MeshBatch.VERTEX_STRIDE, 3);
        vao.SetVertexAttribute(2, 3, VertexAttribPointerType.Float, MeshBatch.VERTEX_STRIDE, 7);
        vao.SetVertexAttribute(3, 3, VertexAttribPointerType.Float, MeshBatch.VERTEX_STRIDE, 10);

        _modelBuffer.Bind();
        for (uint i = 0; i < 4; i++)
        {
            vao.SetVertexAttributePointer(4 + i, 4, VertexAttribPointerType.Float, (uint)sizeof(Matrix4x4), (int)(i * sizeof(float) * 4));
            vao.SetVertexAttributeDivisor(4 + i, 1);
        }

        return arena;
    }

    public void Dispose()
    {
        for (var i = 0; i < _arenas.Count; i++)
        {
            _arenas[i].Dispose();
        }

        _arenas.Clear();
        _modelBuffer.Dispose();
    }

    private sealed class Arena : IDisposable
    {
        public readonly VertexArrayObject32 VertexArrayObject;
        public readonly BufferObject<float> VertexBuffer;
        public readonly BufferObject<uint> IndexBuffer;
        public int VertexCapacity { get; private set; }
        public int IndexCapacity { get; private set; }

        public Arena(VertexArrayObject32 vertexArrayObject)
        {
            VertexArrayObject = vertexArrayObject;
            VertexBuffer = vertexArrayObject.VertexBufferObject;
            IndexBuffer = vertexArrayObject.ElementBufferObject;
        }

        public void Resize(int vertexCapacity, int indexCapacity)
        {
            VertexArrayObject.Bind();
            VertexBuffer.Resize(vertexCapacity * MeshBatch.VERTEX_STRIDE);
            IndexBuffer.Resize(indexCapacity);

            //  The element buffer unbind clears the VAO index binding. Restore it.
            IndexBuffer.Bind();
            VertexCapacity = vertexCapacity;
            IndexCapacity = indexCapacity;
        }

        public void Dispose()
        {
            VertexArrayObject.Dispose();
        }
    }
}

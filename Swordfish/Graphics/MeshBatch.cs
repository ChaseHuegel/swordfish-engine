using Swordfish.Library.Types;

namespace Swordfish.Graphics;

/// <summary>
/// A growable collection of mesh ranges that share one material and one draw state.
/// </summary>
public sealed class MeshBatch : Handle
{
    /// <summary>The number of floats per vertex in a range.</summary>
    public const int VERTEX_STRIDE = 13;

    private const int MAX_ARENA_VERTICES = 1_000_000;
    private const int MAX_ARENA_INDICES = 2_000_000;

    private readonly List<int> _freeSlots = [];
    private readonly List<int> _slotGenerations = [];
    private readonly int _initialVertexCapacity;
    private readonly int _initialIndexCapacity;

    /// <summary>Creates a batch with optional initial arena capacity hints.</summary>
    public MeshBatch(int vertexCapacityHint = 0, int indexCapacityHint = 0)
    {
        _initialVertexCapacity = Math.Clamp(vertexCapacityHint, 0, MAX_ARENA_VERTICES);
        _initialIndexCapacity = Math.Clamp(indexCapacityHint, 0, MAX_ARENA_INDICES);
    }

    internal object SyncRoot { get; } = new();
    internal List<MeshRange> Ranges { get; } = [];
    internal List<MeshBatchArena> Arenas { get; } = [];

    /// <summary>The number of live ranges.</summary>
    public int RangeCount
    {
        get
        {
            lock (SyncRoot)
            {
                var count = 0;
                for (var i = 0; i < Ranges.Count; i++)
                {
                    if (Ranges[i].Active)
                    {
                        count++;
                    }
                }

                return count;
            }
        }
    }

    /// <summary>The number of arenas. Each arena owns one vertex buffer and one index buffer.</summary>
    public int ArenaCount
    {
        get
        {
            lock (SyncRoot)
            {
                return Arenas.Count;
            }
        }
    }

    /// <summary>Increments when the arena layout changes and every range must be re-uploaded.</summary>
    public int Generation { get; private set; }

    /// <summary>Adds one range and returns a stable handle.</summary>
    public MeshRangeHandle AddRange(
        ReadOnlySpan<float> vertexData,
        int vertexCount,
        ReadOnlySpan<uint> indices,
        in BoundingBox bounds
    ) {
        vertexCount = Math.Min(vertexCount, vertexData.Length / VERTEX_STRIDE);

        lock (SyncRoot)
        {
            int slot;
            int generation;
            if (_freeSlots.Count > 0)
            {
                slot = _freeSlots[^1];
                _freeSlots.RemoveAt(_freeSlots.Count - 1);
                generation = ++_slotGenerations[slot];
            }
            else
            {
                slot = Ranges.Count;
                generation = 1;
                Ranges.Add(default);
                _slotGenerations.Add(generation);
            }

            MeshBatchArena arena = Allocate(vertexCount, indices.Length);
            var range = new MeshRange
            {
                Active = true,
                Generation = generation,
                Arena = Arenas.IndexOf(arena),
                VertexOffset = arena.UsedVertices,
                IndexOffset = arena.UsedIndices,
                VertexCapacity = vertexCount,
                IndexCapacity = indices.Length,
                VertexCount = vertexCount,
                IndexCount = indices.Length,
                Bounds = bounds,
                VertexData = [.. vertexData],
                Indices = [.. indices],
                Version = 1,
            };

            arena.UsedVertices += vertexCount;
            arena.UsedIndices += indices.Length;
            Ranges[slot] = range;
            return new MeshRangeHandle(slot, generation);
        }
    }

    /// <summary>Replaces the geometry of a live range. Returns false for a stale handle.</summary>
    public bool UpdateRange(
        MeshRangeHandle handle,
        ReadOnlySpan<float> vertexData,
        int vertexCount,
        ReadOnlySpan<uint> indices,
        in BoundingBox bounds
    ) {
        vertexCount = Math.Min(vertexCount, vertexData.Length / VERTEX_STRIDE);

        lock (SyncRoot)
        {
            if (!TryGetRange(handle, out MeshRange range))
            {
                return false;
            }

            MeshBatchArena arena = Arenas[range.Arena];
            if (vertexCount > range.VertexCapacity || indices.Length > range.IndexCapacity)
            {
                arena.WasteVertices += range.VertexCapacity;
                arena.WasteIndices += range.IndexCapacity;
                arena = Allocate(vertexCount, indices.Length);
                range.Arena = Arenas.IndexOf(arena);
                range.VertexOffset = arena.UsedVertices;
                range.IndexOffset = arena.UsedIndices;
                range.VertexCapacity = vertexCount;
                range.IndexCapacity = indices.Length;
                arena.UsedVertices += vertexCount;
                arena.UsedIndices += indices.Length;
            }

            range.VertexCount = vertexCount;
            range.IndexCount = indices.Length;
            range.Bounds = bounds;
            range.VertexData = [.. vertexData];
            range.Indices = [.. indices];
            range.Version++;
            Ranges[handle.Slot] = range;
            TryCompact();
            return true;
        }
    }

    /// <summary>Removes a live range. Returns false for a stale handle.</summary>
    public bool RemoveRange(MeshRangeHandle handle)
    {
        lock (SyncRoot)
        {
            if (!TryGetRange(handle, out MeshRange range))
            {
                return false;
            }

            MeshBatchArena arena = Arenas[range.Arena];
            arena.WasteVertices += range.VertexCapacity;
            arena.WasteIndices += range.IndexCapacity;
            range.Active = false;
            Ranges[handle.Slot] = range;
            _freeSlots.Add(handle.Slot);
            TryCompact();
            return true;
        }
    }

    /// <summary>Copies the range table for a GL upload. The caller must hold <see cref="SyncRoot"/>.</summary>
    internal MeshRange GetRange(int slot) => Ranges[slot];

    private bool TryGetRange(in MeshRangeHandle handle, out MeshRange range)
    {
        if (handle.Slot < 0 || handle.Slot >= Ranges.Count)
        {
            range = default;
            return false;
        }

        range = Ranges[handle.Slot];
        return range.Active && range.Generation == handle.Generation;
    }

    private MeshBatchArena Allocate(int vertexCount, int indexCount)
    {
        if (Arenas.Count > 0)
        {
            MeshBatchArena last = Arenas[^1];
            int requiredVertices = last.UsedVertices + vertexCount;
            int requiredIndices = last.UsedIndices + indexCount;

            if (requiredVertices <= MAX_ARENA_VERTICES && requiredIndices <= MAX_ARENA_INDICES)
            {
                if (requiredVertices <= last.VertexCapacity && requiredIndices <= last.IndexCapacity)
                {
                    return last;
                }
                
                last.VertexCapacity = Math.Min(MAX_ARENA_VERTICES, Math.Max(last.VertexCapacity * 2, requiredVertices));
                last.IndexCapacity = Math.Min(MAX_ARENA_INDICES, Math.Max(last.IndexCapacity * 2, requiredIndices));
                return last;
            }
        }

        var arena = new MeshBatchArena
        {
            VertexCapacity = Math.Max(vertexCount, Math.Min(MAX_ARENA_VERTICES, Math.Max(_initialVertexCapacity, 4096))),
            IndexCapacity = Math.Max(indexCount, Math.Min(MAX_ARENA_INDICES, Math.Max(_initialIndexCapacity, 8192))),
        };
        Arenas.Add(arena);
        Generation++;
        return arena;
    }

    private void TryCompact()
    {
        var wasteVertices = 0;
        var usedVertices = 0;
        for (var i = 0; i < Arenas.Count; i++)
        {
            wasteVertices += Arenas[i].WasteVertices;
            usedVertices += Arenas[i].UsedVertices;
        }

        if (usedVertices == 0 || wasteVertices * 4 <= usedVertices)
        {
            return;
        }

        for (var arenaIndex = 0; arenaIndex < Arenas.Count; arenaIndex++)
        {
            MeshBatchArena arena = Arenas[arenaIndex];
            var nextVertex = 0;
            var nextIndex = 0;
            for (var slot = 0; slot < Ranges.Count; slot++)
            {
                MeshRange range = Ranges[slot];
                if (!range.Active || range.Arena != arenaIndex)
                {
                    continue;
                }

                range.VertexOffset = nextVertex;
                range.IndexOffset = nextIndex;
                nextVertex += range.VertexCapacity;
                nextIndex += range.IndexCapacity;
                Ranges[slot] = range;
            }

            arena.UsedVertices = nextVertex;
            arena.UsedIndices = nextIndex;
            arena.WasteVertices = 0;
            arena.WasteIndices = 0;
        }

        for (var arenaIndex = Arenas.Count - 1; arenaIndex >= 0; arenaIndex--)
        {
            if (Arenas[arenaIndex].UsedVertices > 0 || Arenas[arenaIndex].UsedIndices > 0)
            {
                continue;
            }

            Arenas.RemoveAt(arenaIndex);
            for (var slot = 0; slot < Ranges.Count; slot++)
            {
                MeshRange range = Ranges[slot];
                if (!range.Active || range.Arena <= arenaIndex)
                {
                    continue;
                }
                
                range.Arena--;
                Ranges[slot] = range;
            }
        }

        Generation++;
    }

    protected override void OnDisposed()
    {
    }
}
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using WaywardBeyond.Data;

namespace WaywardBeyond.Voxels;

/// <summary>
///     A sparse, chunked voxel container. One instance is used by one thread at a time. Do not access two threads at
///     once and take a copy across a thread boundary.
/// </summary>
public sealed class VoxelObject
{
    private static readonly Voxel _outOfBoundsVoxel = new(_ID: 0, _ShapeLight: new ShapeLight(default, lightLevel: 15), _Orientation: 0);

    /// <summary>
    ///     Target for a read outside a resident chunk. It is reset to <see cref="_outOfBoundsVoxel"/> before each
    ///     export, so a write through a returned reference cannot leak into a later read.
    /// </summary>
    private Voxel _outOfBoundsScratch = _outOfBoundsVoxel;

    //  TODO these being internal isn't great
    internal readonly Dictionary<Short3, ChunkData> _chunks;
    internal readonly byte _chunkSize;
    internal readonly int _chunkMask;
    internal readonly int _chunkShift;
    internal readonly int _chunkShift2;
    internal readonly int _voxelsPerChunk;
    
    /// <summary>The default chunk edge length in voxels.</summary>
    public const byte DefaultChunkEdge = 16;

    public VoxelObject(byte chunkSize)
    {
        if (chunkSize <= 0 || (chunkSize & (chunkSize - 1)) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(chunkSize), chunkSize, "Value must be a positive power of 2.");
        }

        _chunks = [];
        _chunkSize = chunkSize;
        _chunkMask = chunkSize - 1;
        _chunkShift = BitOperations.TrailingZeroCount(_chunkSize);
        _chunkShift2 = _chunkShift * 2;
        _voxelsPerChunk = chunkSize * chunkSize * chunkSize;
    }

    public VoxelObject(byte chunkSize, ChunkInfo[] chunkInfos) : this(chunkSize)
    {
        for (var i = 0; i < chunkInfos.Length; i++)
        {
            ChunkInfo chunkInfo = chunkInfos[i];
            Chunk chunk = chunkInfo.Chunk;

            if (chunk.Size != _chunkSize)
            {
                throw new ArgumentException(
                    $"Chunk {chunkInfo.OffsetX},{chunkInfo.OffsetY},{chunkInfo.OffsetZ} has size {chunk.Size}, expected {_chunkSize}.",
                    nameof(chunkInfos)
                );
            }

            if (chunk.Voxels == null || chunk.Voxels.Length != _voxelsPerChunk)
            {
                throw new ArgumentException(
                    $"Chunk {chunkInfo.OffsetX},{chunkInfo.OffsetY},{chunkInfo.OffsetZ} must hold {_voxelsPerChunk} voxels.",
                    nameof(chunkInfos)
                );
            }

            var palette = new VoxelPalette();
            for (var n = 0; n < chunk.Voxels.Length; n++)
            {
                palette.Increment(chunk.Voxels[n].ID);
            }
            
            var chunkOffset = new Short3(chunkInfo.OffsetX, chunkInfo.OffsetY, chunkInfo.OffsetZ);
            var chunkData = new ChunkData(chunkOffset, chunk, voxelObject: this, palette);
            _chunks[chunkOffset] = chunkData;
        }
    }
    
    /// <summary>Number of resident chunks, for sizing containers without touching internals.</summary>
    public int ChunkCount => _chunks.Count;

    /// <summary>The edge length of a chunk in voxels.</summary>
    public byte ChunkSize => _chunkSize;

    /// <summary>Tries to get resident chunk data at the given chunk offset.</summary>
    public bool TryGetChunk(Short3 offset, out ChunkData chunkData) => _chunks.TryGetValue(offset, out chunkData);

    /// <summary>Returns true when the voxel at the given world coordinates has a resident chunk.</summary>
    public bool IsResident(int x, int y, int z) => _chunks.ContainsKey(ToChunkOffset(x, y, z));

    /// <summary>Returns true when any face of the voxel touches a non-resident chunk.</summary>
    public bool IsAdjacentToNonResident(int x, int y, int z)
    {
        int localX = x & _chunkMask;
        int localY = y & _chunkMask;
        int localZ = z & _chunkMask;

        if (localX == 0 && !IsResident(x - 1, y, z)) return true;
        if (localX == _chunkMask && !IsResident(x + 1, y, z)) return true;
        if (localY == 0 && !IsResident(x, y - 1, z)) return true;
        if (localY == _chunkMask && !IsResident(x, y + 1, z)) return true;
        if (localZ == 0 && !IsResident(x, y, z - 1)) return true;
        if (localZ == _chunkMask && !IsResident(x, y, z + 1)) return true;
        return false;
    }

    /// <summary>Calculates the chunk offset containing the given world voxel coordinates.</summary>
    public Short3 ToChunkOffset(int x, int y, int z) => new((short)(x >> _chunkShift), (short)(y >> _chunkShift), (short)(z >> _chunkShift));
    
    public void Set(int x, int y, int z, Voxel voxel)
    {
        var chunkX = (short)(x >> _chunkShift);
        var chunkY = (short)(y >> _chunkShift);
        var chunkZ = (short)(z >> _chunkShift);
        var chunkOffset = new Short3(chunkX, chunkY, chunkZ);
        
        if (!_chunks.TryGetValue(chunkOffset, out ChunkData chunkData))
        {
            var chunk = new Chunk(_chunkSize, new Voxel[_voxelsPerChunk]);
            chunkData = new ChunkData(chunkOffset, chunk, voxelObject: this);
            _chunks[chunkOffset] = chunkData;
        }
        
        if (x < 0)
        {
            x += _chunkSize * Math.Abs((int)chunkX);
        }

        if (y < 0)
        {
            y += _chunkSize * Math.Abs((int)chunkY);
        }

        if (z < 0)
        {
            z += _chunkSize * Math.Abs((int)chunkZ);
        }
        
        int localX = x & _chunkMask;
        int localY = y & _chunkMask;
        int localZ = z & _chunkMask;
        
        int index = localX + (localY << _chunkShift) + (localZ << _chunkShift2);
        var chunkVoxel = new ChunkVoxel(chunkData, ref chunkData.Data.Voxels[index], index);
        chunkVoxel.Set(voxel);
    }

    /// <summary>Updates the light level of the voxel at the given world coordinates. The level must be 0..15.</summary>
    public void SetLight(int x, int y, int z, int lightLevel)
    {
        Debug.Assert(lightLevel is >= 0 and <= 15, "Light level must be in the range 0..15.");

        var chunkX = (short)(x >> _chunkShift);
        var chunkY = (short)(y >> _chunkShift);
        var chunkZ = (short)(z >> _chunkShift);
        var chunkOffset = new Short3(chunkX, chunkY, chunkZ);
        
        if (!_chunks.TryGetValue(chunkOffset, out ChunkData chunkData))
        {
            return;
        }
        
        if (x < 0)
        {
            x += _chunkSize * Math.Abs((int)chunkX);
        }

        if (y < 0)
        {
            y += _chunkSize * Math.Abs((int)chunkY);
        }

        if (z < 0)
        {
            z += _chunkSize * Math.Abs((int)chunkZ);
        }
        
        int localX = x & _chunkMask;
        int localY = y & _chunkMask;
        int localZ = z & _chunkMask;
        
        int index = localX + (localY << _chunkShift) + (localZ << _chunkShift2);
        ref Voxel current = ref chunkData.Data.Voxels[index];
        ShapeLight shapeLight = current.GetShapeLight();
        current.ShapeLight = new ShapeLight(shapeLight.Shape, lightLevel);
    }

    public Voxel Get(int x, int y, int z)
    {
        return GetUnsafe(x, y, z);
    }
    
    internal ref Voxel GetUnsafe(int x, int y, int z)
    {
        var chunkX = (short)(x >> _chunkShift);
        var chunkY = (short)(y >> _chunkShift);
        var chunkZ = (short)(z >> _chunkShift);
        var chunkOffset = new Short3(chunkX, chunkY, chunkZ);
        
        if (!_chunks.TryGetValue(chunkOffset, out ChunkData chunkData))
        {
            return ref GetOutOfBoundsVoxel();
        }
        
        if (x < 0)
        {
            x += _chunkSize * Math.Abs((int)chunkX);
        }

        if (y < 0)
        {
            y += _chunkSize * Math.Abs((int)chunkY);
        }

        if (z < 0)
        {
            z += _chunkSize * Math.Abs((int)chunkZ);
        }
        
        int localX = x & _chunkMask;
        int localY = y & _chunkMask;
        int localZ = z & _chunkMask;
        
        int index = localX + (localY << _chunkShift) + (localZ << _chunkShift2);
        return ref chunkData.Data.Voxels[index];
    }
    
    public VoxelSample Sample(int x, int y, int z)
    {
        var chunkX = (short)(x >> _chunkShift);
        var chunkY = (short)(y >> _chunkShift);
        var chunkZ = (short)(z >> _chunkShift);
        var chunkOffset = new Short3(chunkX, chunkY, chunkZ);
        
        if (!_chunks.TryGetValue(chunkOffset, out ChunkData chunkData))
        {
            return new VoxelSample(
                chunkOffset: default,
                chunkCoords: default,
                coords: default,
                center: ref GetOutOfBoundsVoxel(),
                left: ref GetOutOfBoundsVoxel(),
                right: ref GetOutOfBoundsVoxel(),
                ahead: ref GetOutOfBoundsVoxel(),
                behind: ref GetOutOfBoundsVoxel(),
                above: ref GetOutOfBoundsVoxel(),
                below: ref GetOutOfBoundsVoxel()
            );
        }
        
        int localX = x & _chunkMask;
        int localY = y & _chunkMask;
        int localZ = z & _chunkMask;
        
        if (localX < 0)
        {
            localX += _chunkSize;
        }

        if (localY < 0)
        {
            localY += _chunkSize;
        }

        if (localZ < 0)
        {
            localZ += _chunkSize;
        }

        Voxel[] voxels = chunkData.Data.Voxels;
        
        int index = localX + (localY << _chunkShift) + (localZ << _chunkShift2);
        var sample = new VoxelSample
        {
            Center = ref voxels[index],
            Left = ref GetNeighbor(-1, 0, 0),
            Right = ref GetNeighbor(1, 0, 0),
            Above = ref GetNeighbor(0, 1, 0),
            Below = ref GetNeighbor(0, -1, 0),
            Ahead = ref GetNeighbor(0, 0, 1),
            Behind = ref GetNeighbor(0, 0, -1),
        };

        ref Voxel GetNeighbor(int offsetX, int offsetY, int offsetZ)
        {
            int nX = localX + offsetX;
            int nY = localY + offsetY;
            int nZ = localZ + offsetZ;

            if (nX >= 0 && nY >= 0 && nZ >= 0 && nX < _chunkSize && nY < _chunkSize && nZ < _chunkSize)
            {
                int neighborIndex = nX + (nY << _chunkShift) + (nZ << _chunkShift2);
                if (neighborIndex >= 0 && neighborIndex < _voxelsPerChunk)
                {
                    return ref voxels[neighborIndex];
                }
            }

            return ref GetUnsafe(x + offsetX, y + offsetY, z + offsetZ);
        }
        
        return sample;
    }

    /// <summary>Returns a ref to a voxel that does not exist in any resident chunk.</summary>
    private ref Voxel GetOutOfBoundsVoxel()
    {
        _outOfBoundsScratch = _outOfBoundsVoxel;
        return ref _outOfBoundsScratch;
    }
    
    public Enumerator GetEnumerator()
    {
        return new Enumerator(_chunks);
    }

    public ref struct Enumerator(in Dictionary<Short3, ChunkData> chunks)
    {
        public ChunkData Current => _chunkEnumerator.Current;
        
        private Dictionary<Short3, ChunkData>.ValueCollection.Enumerator _chunkEnumerator = chunks.Values.GetEnumerator();
        
        public bool MoveNext()
        {
            return _chunkEnumerator.MoveNext();
        }
    }
    
    /// <summary>Returns a copy of every resident chunk. The caller owns the voxel arrays.</summary>
    public ChunkInfo[] GetChunkInfos()
    {
        var chunkInfos = new ChunkInfo[_chunks.Count];
        var index = 0;
        foreach (ChunkData chunkData in _chunks.Values)
        {
            chunkInfos[index] = chunkData.ToChunkInfo();
            index++;
        }

        return chunkInfos;
    }
}

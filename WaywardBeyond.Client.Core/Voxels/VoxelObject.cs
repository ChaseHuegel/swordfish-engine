using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using WaywardBeyond.Client.Core.Numerics;
using WaywardBeyond.Client.Core.Voxels.Models;

namespace WaywardBeyond.Client.Core.Voxels;

public sealed class VoxelObject : IDisposable
{
    private static Voxel _emptyVoxel;
    
    internal readonly ReaderWriterLockSlim _lock;
    internal readonly Dictionary<Short3, ChunkData> _chunks;
    internal readonly byte _chunkSize;
    internal readonly int _chunkMask;
    internal readonly int _chunkShift;
    internal readonly int _chunkShift2;
    internal readonly int _voxelsPerChunk;
    
    public VoxelObject(byte chunkSize)
    {
        if (chunkSize <= 0 || (chunkSize & (chunkSize - 1)) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(chunkSize), chunkSize, "Value must be a positive power of 2.");
        }

        _lock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
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

            var palette = new VoxelPalette();
            for (var n = 0; n < chunkInfo.Chunk.Voxels.Length; n++)
            {
                palette.Increment(chunkInfo.Chunk.Voxels[n].ID);
            }
            
            var chunkOffset = new Short3(chunkInfo.OffsetX, chunkInfo.OffsetY, chunkInfo.OffsetZ);
            var chunkData = new ChunkData(chunkOffset, chunkInfo.Chunk, voxelObject: this, palette);
            _chunks[chunkOffset] = chunkData;
        }
    }

    public void Dispose()
    {
        _lock.Dispose();
    }
    
    public void Set(int x, int y, int z, Voxel voxel)
    {
        _lock.EnterWriteLock();

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
            x += _chunkSize * Math.Abs(chunkX);
        }

        if (y < 0)
        {
            y += _chunkSize * Math.Abs(chunkY);
        }

        if (z < 0)
        {
            z += _chunkSize * Math.Abs(chunkZ);
        }
        
        int localX = x & _chunkMask;
        int localY = y & _chunkMask;
        int localZ = z & _chunkMask;
        
        int index = localX + (localY << _chunkShift) + (localZ << _chunkShift2);
        var chunkVoxel = new ChunkVoxel(chunkData, ref chunkData.Data.Voxels[index]);
        chunkVoxel.Set(voxel);

        _lock.ExitWriteLock();
    }

    public Voxel Get(int x, int y, int z)
    {
        _lock.EnterReadLock();
        Voxel voxel = GetUnsafe(x, y, z);
        _lock.ExitReadLock();
        return voxel;
    }
    
    internal ref Voxel GetUnsafe(int x, int y, int z)
    {

        var chunkX = (short)(x >> _chunkShift);
        var chunkY = (short)(y >> _chunkShift);
        var chunkZ = (short)(z >> _chunkShift);
        var chunkOffset = new Short3(chunkX, chunkY, chunkZ);
        
        if (!_chunks.TryGetValue(chunkOffset, out ChunkData chunkData))
        {
            _emptyVoxel = new Voxel();
            return ref _emptyVoxel;
        }
        
        if (x < 0)
        {
            x += _chunkSize * Math.Abs(chunkX);
        }

        if (y < 0)
        {
            y += _chunkSize * Math.Abs(chunkY);
        }

        if (z < 0)
        {
            z += _chunkSize * Math.Abs(chunkZ);
        }
        
        int localX = x & _chunkMask;
        int localY = y & _chunkMask;
        int localZ = z & _chunkMask;
        
        int index = localX + (localY << _chunkShift) + (localZ << _chunkShift2);
        return ref chunkData.Data.Voxels[index];
    }
    
    public VoxelSample Sample(int x, int y, int z)
    {
        _lock.EnterReadLock();

        var chunkX = (short)(x >> _chunkShift);
        var chunkY = (short)(y >> _chunkShift);
        var chunkZ = (short)(z >> _chunkShift);
        var chunkOffset = new Short3(chunkX, chunkY, chunkZ);
        
        if (!_chunks.TryGetValue(chunkOffset, out ChunkData chunkData))
        {
            _lock.ExitReadLock();
            _emptyVoxel = new Voxel();
            return new VoxelSample(
                chunkOffset: default,
                chunkCoords: default,
                coords: default,
                center: ref _emptyVoxel,
                left: ref _emptyVoxel,
                right: ref _emptyVoxel,
                ahead: ref _emptyVoxel,
                behind: ref _emptyVoxel,
                above: ref _emptyVoxel,
                below: ref _emptyVoxel
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
            Ahead = ref GetNeighbor(0, 0, -1),
            Behind = ref GetNeighbor(0, 0, 1),
        };

        ref Voxel GetNeighbor(int offsetX, int offsetY, int offsetZ)
        {
            int nX = localX + offsetX;
            int nY = localY + offsetY;
            int nZ = localZ + offsetZ;

            if (nX > 0 && nY > 0 && nZ > 0 && nX < _chunkSize && nY < _chunkSize && nZ < _chunkSize)
            {
                int neighborIndex = nX + (nY << _chunkShift) + (nZ << _chunkShift2);
                if (neighborIndex >= 0 && neighborIndex < _voxelsPerChunk)
                {
                    return ref voxels[neighborIndex];
                }
            }

            return ref GetUnsafe(x + offsetX, y + offsetY, z + offsetZ);
        }
        
        _lock.ExitReadLock();
        return sample;
    }
    
    public Enumerator GetEnumerator()
    {
        _lock.EnterUpgradeableReadLock();
        _lock.EnterWriteLock();
        return new Enumerator(_chunks, _lock);
    }

    public ref struct Enumerator(in Dictionary<Short3, ChunkData> chunks, in ReaderWriterLockSlim @lock) : IDisposable
    {
        public ChunkData Current => _chunkEnumerator.Current;
        
        private readonly ReaderWriterLockSlim _lock = @lock;
        private Dictionary<Short3, ChunkData>.ValueCollection.Enumerator _chunkEnumerator = chunks.Values.GetEnumerator();
        
        public void Dispose()
        {
            _lock.ExitWriteLock();
            _lock.ExitUpgradeableReadLock();
        }

        public bool MoveNext()
        {
            return _chunkEnumerator.MoveNext();
        }
    }
    
    public ChunkInfo[] GetChunkInfos()
    {
        _lock.EnterReadLock();
        ChunkInfo[] chunkInfos = _chunks.Values.Select(chunkData => chunkData.ToChunkInfo()).ToArray();
        _lock.ExitReadLock();
        return chunkInfos;
    }
}
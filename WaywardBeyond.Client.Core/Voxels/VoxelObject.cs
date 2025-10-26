using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using WaywardBeyond.Client.Core.Numerics;

namespace WaywardBeyond.Client.Core.Voxels;

public sealed class VoxelObject : IDisposable
{
    private readonly ReaderWriterLockSlim _lock;
    private readonly Dictionary<Short3, Chunk> _chunks;
    private readonly byte _chunkSize;
    private readonly int _chunkMask;
    private readonly int _chunkShift;
    private readonly int _chunkShift2;
    private readonly int _voxelsPerChunk;
    
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
        
        if (!_chunks.TryGetValue(chunkOffset, out Chunk chunk))
        {
            _chunks[chunkOffset] = new Chunk(_chunkSize, new Voxel[_voxelsPerChunk]);
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
        
        int index = localX + (localY << _chunkShift) + (localZ << _chunkShift2);
        chunk.Voxels[index] = voxel;

        _lock.ExitWriteLock();
    }
    
    public Voxel Get(int x, int y, int z)
    {
        _lock.EnterReadLock();

        var chunkX = (short)(x >> _chunkShift);
        var chunkY = (short)(y >> _chunkShift);
        var chunkZ = (short)(z >> _chunkShift);
        var chunkOffset = new Short3(chunkX, chunkY, chunkZ);
        
        if (!_chunks.TryGetValue(chunkOffset, out Chunk chunk))
        {
            return new Voxel();
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
        
        int index = localX + (localY << _chunkShift) + (localZ << _chunkShift2);
        Voxel voxel = chunk.Voxels[index];
        
        _lock.ExitReadLock();
        return voxel;
    }

    public ReadWriteEnumerator GetEnumerator()
    {
        _lock.EnterWriteLock();
        _lock.EnterReadLock();
        return new ReadWriteEnumerator(_chunks, _lock);
    }
    
    public ReadEnumerator GetReadEnumerator()
    {
        _lock.EnterWriteLock();
        return new ReadEnumerator(_chunks, _lock);
    }
    
    public ref struct ReadWriteEnumerator(in Dictionary<Short3, Chunk> chunks, in ReaderWriterLockSlim @lock)
    {
        public ref Voxel Current => ref _currentVoxels![_voxelIndex];
        
        private Dictionary<Short3, Chunk>.Enumerator _chunkEnumerator = chunks.GetEnumerator();
        private readonly ReaderWriterLockSlim _lock = @lock;

        private Voxel[]? _currentVoxels = null;
        private int _voxelIndex = -1;
        
        public void Dispose()
        {
            _lock.ExitReadLock();
            _lock.ExitWriteLock();
        }

        public bool MoveNext()
        {
            _voxelIndex++;
            if (_currentVoxels != null && _voxelIndex < _currentVoxels.Length)
            {
                return true;
            }

            while (_chunkEnumerator.MoveNext())
            {
                Chunk chunk = _chunkEnumerator.Current.Value;
                _currentVoxels = chunk.Voxels;
                _voxelIndex = 0;
             
                if (_currentVoxels.Length > 0)
                {
                    return true;
                }
            }

            _currentVoxels = null;
            return false;
        }
    }
    
    public ref struct ReadEnumerator(in Dictionary<Short3, Chunk> chunks, in ReaderWriterLockSlim @lock)
    {
        public Voxel Current => _currentVoxels![_voxelIndex];
        
        private Dictionary<Short3, Chunk>.Enumerator _chunkEnumerator = chunks.GetEnumerator();
        private readonly ReaderWriterLockSlim _lock = @lock;
        
        private Voxel[]? _currentVoxels = null;
        private int _voxelIndex = -1;
        
        public void Dispose()
        {
            _lock.ExitReadLock();
        }

        public bool MoveNext()
        {
            _voxelIndex++;
            if (_currentVoxels != null && _voxelIndex < _currentVoxels.Length)
            {
                return true;
            }

            while (_chunkEnumerator.MoveNext())
            {
                Chunk chunk = _chunkEnumerator.Current.Value;
                _currentVoxels = chunk.Voxels;
                _voxelIndex = 0;
             
                if (_currentVoxels.Length > 0)
                {
                    return true;
                }
            }

            _currentVoxels = null;
            return false;
        }
    }
}
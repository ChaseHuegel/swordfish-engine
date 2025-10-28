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
            chunk = new Chunk(_chunkSize, new Voxel[_voxelsPerChunk]);
            _chunks[chunkOffset] = chunk;
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
        Voxel voxel = GetUnsafe(x, y, z);
        _lock.ExitReadLock();
        return voxel;
    }
    
    private Voxel GetUnsafe(int x, int y, int z)
    {

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
        
        return voxel;
    }
    
    public VoxelSample Sample(int x, int y, int z)
    {
        _lock.EnterReadLock();

        var chunkX = (short)(x >> _chunkShift);
        var chunkY = (short)(y >> _chunkShift);
        var chunkZ = (short)(z >> _chunkShift);
        var chunkOffset = new Short3(chunkX, chunkY, chunkZ);
        
        if (!_chunks.TryGetValue(chunkOffset, out Chunk chunk))
        {
            return new VoxelSample();
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

        Voxel[] voxels = chunk.Voxels;

        
        int index = localX + (localY << _chunkShift) + (localZ << _chunkShift2);
        var sample = new VoxelSample
        {
            Center = voxels[index],
            Left = GetNeighbor(-1, 0, 0),
            Right = GetNeighbor(1, 0, 0),
            Above = GetNeighbor(0, 1, 0),
            Below = GetNeighbor(0, -1, 0),
            Ahead = GetNeighbor(0, 0, -1),
            Behind = GetNeighbor(0, 0, 1)
        };

        Voxel GetNeighbor(int offsetX, int offsetY, int offsetZ)
        {
            int neighborIndex = localX + offsetX + ((localY + offsetY) << _chunkShift) + ((localZ + offsetZ) << _chunkShift2);
            if (neighborIndex >= 0 && neighborIndex < _voxelsPerChunk)
            {
                return voxels[neighborIndex];
            }

            return GetUnsafe(x + offsetX, y + offsetY, z + offsetZ);
        }
        
        _lock.ExitReadLock();
        return sample;
    }
    
    public ReadWriteEnumerator GetEnumerator()
    {
        _lock.EnterUpgradeableReadLock();
        _lock.EnterWriteLock();
        return new ReadWriteEnumerator(_chunks, _lock);
    }
    
    public ref struct ReadWriteEnumerator(in Dictionary<Short3, Chunk> chunks, in ReaderWriterLockSlim @lock) : IDisposable
    {
        public ref Voxel Current => ref _currentVoxels![_voxelIndex];
        
        private Dictionary<Short3, Chunk>.Enumerator _chunkEnumerator = chunks.GetEnumerator();
        private readonly ReaderWriterLockSlim _lock = @lock;

        private Voxel[]? _currentVoxels = null;
        private int _voxelIndex = -1;
        
        public void Dispose()
        {
            _lock.ExitWriteLock();
            _lock.ExitUpgradeableReadLock();
        }

        public bool MoveNext()
        {
            if (_currentVoxels != null && _voxelIndex + 1 < _currentVoxels.Length)
            {
                _voxelIndex++;
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
    
    public ref struct ReadEnumerator(in Dictionary<Short3, Chunk> chunks, in ReaderWriterLockSlim @lock) : IDisposable
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
            if (_currentVoxels != null && _voxelIndex + 1 < _currentVoxels.Length)
            {
                _voxelIndex++;
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

    public Sampler GetSampler()
    {
        return new Sampler(this);
    }

    public readonly ref struct Sampler(VoxelObject voxelObject)
    {
        public SampleEnumerator GetEnumerator()
        {
            return new SampleEnumerator(voxelObject);
        }
    }
    
    public ref struct SampleEnumerator : IDisposable
    {
        public VoxelSample Current { get; private set; }

        private readonly VoxelObject _voxelObject;
        private Dictionary<Short3, Chunk>.Enumerator _chunkEnumerator;
        private readonly ReaderWriterLockSlim _lock;
        
        private Voxel[]? _currentVoxels = null;
        private int _voxelIndex = -1;

        public SampleEnumerator(VoxelObject voxelObject)
        {
            _voxelObject = voxelObject;
            _chunkEnumerator = voxelObject._chunks.GetEnumerator();
            _lock = voxelObject._lock;
            
            _lock.EnterReadLock();
        }

        public void Dispose()
        {
            _lock.ExitReadLock();
        }

        public bool MoveNext()
        {
            if (_currentVoxels != null && _voxelIndex + 1 < _currentVoxels.Length)
            {
                int chunkShift = _voxelObject._chunkShift;
                int chunkShift2 = _voxelObject._chunkShift2;
                int voxelsPerChunk = _voxelObject._voxelsPerChunk;
                Voxel[] voxels = _currentVoxels;
                VoxelObject voxelObject = _voxelObject;

                int x = _voxelIndex & ((1 << chunkShift) - 1);
                int y = (_voxelIndex >> chunkShift) & ((1 << (chunkShift2 - chunkShift)) - 1);
                int z = _voxelIndex >> chunkShift2;

                var sample = new VoxelSample
                {
                    Center = _currentVoxels![_voxelIndex],
                    Left = GetNeighbor(-1, 0, 0),
                    Right = GetNeighbor(1, 0, 0),
                    Above = GetNeighbor(0, 1, 0),
                    Below = GetNeighbor(0, -1, 0),
                    Ahead = GetNeighbor(0, 0, -1),
                    Behind = GetNeighbor(0, 0, 1)
                };

                Voxel GetNeighbor(int offsetX, int offsetY, int offsetZ)
                {
                    int neighborIndex = x + offsetX + ((y + offsetY) << chunkShift) +
                                        ((z + offsetZ) << chunkShift2);
                    if (neighborIndex >= 0 && neighborIndex < voxelsPerChunk)
                    {
                        return voxels![neighborIndex];
                    }

                    return voxelObject.GetUnsafe(x + offsetX, y + offsetY, z + offsetZ);
                }

                Current = sample;
                _voxelIndex++;
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
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using WaywardBeyond.Client.Core.Numerics;

namespace WaywardBeyond.Client.Core.Voxels;

public sealed class VoxelObject : IDisposable
{
    private static Voxel _emptyVoxel;
    
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
    
    private ref Voxel GetUnsafe(int x, int y, int z)
    {

        var chunkX = (short)(x >> _chunkShift);
        var chunkY = (short)(y >> _chunkShift);
        var chunkZ = (short)(z >> _chunkShift);
        var chunkOffset = new Short3(chunkX, chunkY, chunkZ);
        
        if (!_chunks.TryGetValue(chunkOffset, out Chunk chunk))
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
        return ref chunk.Voxels[index];
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

        Voxel[] voxels = chunk.Voxels;
        
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
            int neighborIndex = localX + offsetX + ((localY + offsetY) << _chunkShift) + ((localZ + offsetZ) << _chunkShift2);
            if (neighborIndex >= 0 && neighborIndex < _voxelsPerChunk)
            {
                return ref voxels[neighborIndex];
            }

            return ref GetUnsafe(x + offsetX, y + offsetY, z + offsetZ);
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
                return true;
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
                return true;
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
        public VoxelSample Current => GetCurrentSample();

        private readonly VoxelObject _voxelObject;
        private Dictionary<Short3, Chunk>.Enumerator _chunkEnumerator;
        private readonly ReaderWriterLockSlim _lock;
        
        private Int3 _chunkPos;
        private Int3 _chunkWorldCoords;
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
                _voxelIndex++;
                return true;
            }

            while (_chunkEnumerator.MoveNext())
            {
                KeyValuePair<Short3, Chunk> kvp = _chunkEnumerator.Current;
                Chunk chunk = kvp.Value;
                _chunkPos = new Int3(kvp.Key.X, kvp.Key.Y, kvp.Key.Z);
                _chunkWorldCoords = new Int3(kvp.Key.X * chunk.Size, kvp.Key.Y * chunk.Size, kvp.Key.Z * chunk.Size);
                _currentVoxels = chunk.Voxels;
                _voxelIndex = 0;
                return true;
            }

            _currentVoxels = null;
            return false;
        }

        private VoxelSample GetCurrentSample()
        {
            int x = _voxelIndex & ((1 << _voxelObject._chunkShift) - 1);
            int y = (_voxelIndex >> _voxelObject._chunkShift) & ((1 << (_voxelObject._chunkShift2 - _voxelObject._chunkShift)) - 1);
            int z = _voxelIndex >> _voxelObject._chunkShift2;

            return new VoxelSample(
                chunkOffset: _chunkWorldCoords,
                chunkCoords: _chunkPos,
                coords: new Int3(x, y, z),
                center: ref _currentVoxels![_voxelIndex],
                left: ref GetNeighbor(x, y, z, -1, 0, 0),
                right: ref GetNeighbor(x, y, z, 1, 0, 0),
                ahead: ref GetNeighbor(x, y, z, 0, 0, 1),
                behind: ref GetNeighbor(x, y, z, 0, 0, -1),
                above: ref GetNeighbor(x, y, z, 0, 1, 0),
                below: ref GetNeighbor(x, y, z, 0, -1, 0)
            );
        }

        private ref Voxel GetNeighbor(int x, int y, int z, int offsetX, int offsetY, int offsetZ)
        {
            int nX = x + offsetX;
            int nY = y + offsetY;
            int nZ = z + offsetZ;

            if (nX < 0 || nY < 0 || nZ < 0)
            {
                return ref _voxelObject.GetUnsafe(nX + _chunkWorldCoords.X, nY + _chunkWorldCoords.Y, nZ + _chunkWorldCoords.Z);
            }

            int neighborIndex = nX + (nY << _voxelObject._chunkShift) + (nZ << _voxelObject._chunkShift2);
            if (neighborIndex < 0 || neighborIndex >= _voxelObject._voxelsPerChunk)
            {
                return ref _voxelObject.GetUnsafe(nX + _chunkWorldCoords.X, nY + _chunkWorldCoords.Y, nZ + _chunkWorldCoords.Z);
            }

            return ref _currentVoxels![neighborIndex];
        }
    }
}
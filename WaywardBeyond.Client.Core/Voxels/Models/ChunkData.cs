using System;
using System.Collections.Generic;
using System.Threading;
using WaywardBeyond.Client.Core.Numerics;

namespace WaywardBeyond.Client.Core.Voxels.Models;

public readonly struct ChunkData(in Short3 coords, in Chunk chunk)
{
    public readonly Short3 Coords = coords;
    public readonly Chunk Chunk = chunk;
    public readonly VoxelPalette Palette = new(chunk.Voxels.Length);

    private ReaderWriterLockSlim _lock;

    public ReadWriteEnumerator GetVoxels()
    {
        _lock.EnterUpgradeableReadLock();
        _lock.EnterWriteLock();
        return new ReadWriteEnumerator(_chunks, _lock);
    }
    
    public ref struct ReadWriteEnumerator(in Dictionary<Short3, ChunkData> chunks, in ReaderWriterLockSlim @lock) : IDisposable
    {
        public ref Voxel Current => ref _currentVoxels![_voxelIndex];
        
        private Dictionary<Short3, ChunkData>.Enumerator _chunkEnumerator = chunks.GetEnumerator();
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
                ChunkData chunkData = _chunkEnumerator.Current.Value;
                _currentVoxels = chunkData.Chunk.Voxels;
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
        private Dictionary<Short3, ChunkData>.Enumerator _chunkEnumerator;
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
                KeyValuePair<Short3, ChunkData> kvp = _chunkEnumerator.Current;
                ChunkData chunkData = kvp.Value;
                _chunkPos = new Int3(kvp.Key.X, kvp.Key.Y, kvp.Key.Z);
                _chunkWorldCoords = new Int3(kvp.Key.X * chunkData.Chunk.Size, kvp.Key.Y * chunkData.Chunk.Size, kvp.Key.Z * chunkData.Chunk.Size);
                _currentVoxels = chunkData.Chunk.Voxels;
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
                ahead: ref GetNeighbor(x, y, z, 0, 0, -1),
                behind: ref GetNeighbor(x, y, z, 0, 0, 1),
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
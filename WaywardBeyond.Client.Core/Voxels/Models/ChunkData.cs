using System;
using System.Threading;
using WaywardBeyond.Client.Core.Numerics;

namespace WaywardBeyond.Client.Core.Voxels.Models;

public readonly struct ChunkData(in Short3 coords, in Chunk data, in VoxelObject voxelObject, in VoxelPalette? palette = null)
{
    public readonly Short3 Coords = coords;
    public readonly Chunk Data = data;
    public readonly VoxelPalette Palette = palette ?? new VoxelPalette(data.Voxels.Length);
    
    private readonly VoxelObject _voxelObject = voxelObject;

    public ChunkInfo ToChunkInfo()
    {
        return new ChunkInfo(Coords.X, Coords.Y, Coords.Z, Data);
    }

    public Enumerator GetEnumerator()
    {
        Voxel[] voxels = Data.Voxels;
        return new Enumerator(voxels);
    }
    
    public ref struct Enumerator : IDisposable
    {
        public ref Voxel Current => ref _voxels[_index];

        private Voxel[] _voxels;
        private int _index = -1;

        public Enumerator(Voxel[] voxels)
        {
            _voxels = voxels;
            Monitor.Enter(voxels);
        }

        public void Dispose()
        {
            Monitor.Exit(_voxels);
        }

        public bool MoveNext()
        {
            if (_index + 1 >= _voxels.Length)
            {
                return false;
            }

            _index++;
            return true;
        }
    }
    
    public Sampler GetSampler()
    {
        return new Sampler(chunkData: this, _voxelObject);
    }
    
    public readonly ref struct Sampler(ChunkData chunkData, VoxelObject voxelObject)
    {
        public SampleEnumerator GetEnumerator()
        {
            return new SampleEnumerator(chunkData, voxelObject);
        }
    }
    
    public ref struct SampleEnumerator
    {
        public VoxelSample Current => GetCurrentSample();

        private readonly VoxelObject _voxelObject;
        
        private readonly Int3 _chunkPos;
        private readonly Int3 _chunkWorldCoords;
        private readonly Voxel[] _currentVoxels;
        private int _voxelIndex = -1;

        public SampleEnumerator(ChunkData chunkData, VoxelObject voxelObject)
        {
            _voxelObject = voxelObject;
            _chunkPos = new Int3(chunkData.Coords.X, chunkData.Coords.Y, chunkData.Coords.Z);
            _chunkWorldCoords = new Int3(chunkData.Coords.X * chunkData.Data.Size, chunkData.Coords.Y * chunkData.Data.Size, chunkData.Coords.Z * chunkData.Data.Size);
            _currentVoxels = chunkData.Data.Voxels;
        }

        public bool MoveNext()
        {
            if (_voxelIndex + 1 >= _currentVoxels.Length)
            {
                return false;
            }

            _voxelIndex++;
            return true;
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
                center: ref _currentVoxels[_voxelIndex],
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

            return ref _currentVoxels[neighborIndex];
        }
    }
}
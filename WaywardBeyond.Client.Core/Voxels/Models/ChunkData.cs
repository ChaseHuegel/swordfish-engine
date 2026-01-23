using System;
using System.Threading;
using WaywardBeyond.Client.Core.Numerics;

namespace WaywardBeyond.Client.Core.Voxels.Models;

public readonly struct ChunkData(in Short3 coords, in Chunk data, in VoxelObject voxelObject, in VoxelPalette? palette = null)
{
    /// <summary>
    ///     Lookup table of relative X/Y/Z oriented offsets to neighbors.
    ///     This is indexed with: [Orientation][Neighbor]
    /// </summary>
    private static readonly Int3[][] _neighborOffsetLookup = new Int3[64][];

    /// <summary>
    ///     Relative X/Y/Z offsets to neighbors for an Identity orientation.
    /// </summary>
    private static readonly Int3[] _neighborOffset =
    {
        new(-1, 0,  0), // Left
        new( 1, 0,  0), // Right
        new( 0, 0,  1), // Ahead
        new( 0, 0, -1), // Behind
        new( 0, 1,  0), // Above
        new( 0,-1,  0), // Below
    };
    
    static ChunkData()
    {
        //  Precalculate the lookup table of neighbor offsets
        for (byte orientationValue = 0; orientationValue < 64; orientationValue++)
        {
            var orientation = new Orientation(orientationValue);

            _neighborOffsetLookup[orientationValue] = new Int3[6];
            for (var neighbor = 0; neighbor < 6; neighbor++)
            {
                _neighborOffsetLookup[orientationValue][neighbor] = RotateOffset(_neighborOffset[neighbor], orientation);
            }
        }
        
        Int3 RotateOffset(Int3 offset, Orientation orientation)
        {
            Int3 result = offset;
        
            for (var i = 0; i < orientation.PitchRotations; i++)
            {
                result = new Int3(result.X, -result.Z, result.Y);
            }
        
            for (var i = 0; i < orientation.YawRotations; i++)
            {
                result = new Int3(result.Z, result.Y, -result.X);
            }
            
            for (var i = 0; i < orientation.RollRotations; i++)
            {
                result = new Int3(-result.Y, result.X, result.Z);
            }

            return result;
        }
    }
    
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

            ref Voxel center = ref _currentVoxels[_voxelIndex];
            
            Int3 leftOffset = _neighborOffsetLookup[center.Orientation][(int)Neighbor.Left];
            Int3 rightOffset = _neighborOffsetLookup[center.Orientation][(int)Neighbor.Right];
            Int3 aheadOffset = _neighborOffsetLookup[center.Orientation][(int)Neighbor.Ahead];
            Int3 behindOffset = _neighborOffsetLookup[center.Orientation][(int)Neighbor.Behind];
            Int3 aboveOffset = _neighborOffsetLookup[center.Orientation][(int)Neighbor.Above];
            Int3 belowOffset = _neighborOffsetLookup[center.Orientation][(int)Neighbor.Below];

            return new VoxelSample(
                chunkOffset: _chunkWorldCoords,
                chunkCoords: _chunkPos,
                coords: new Int3(x, y, z),
                center: ref center,
                left:   ref GetNeighbor(x, y, z, leftOffset.X,   leftOffset.Y,   leftOffset.Z),
                right:  ref GetNeighbor(x, y, z, rightOffset.X,  rightOffset.Y,  rightOffset.Z),
                ahead:  ref GetNeighbor(x, y, z, aheadOffset.X,  aheadOffset.Y,  aheadOffset.Z),
                behind: ref GetNeighbor(x, y, z, behindOffset.X, behindOffset.Y, behindOffset.Z),
                above:  ref GetNeighbor(x, y, z, aboveOffset.X,  aboveOffset.Y,  aboveOffset.Z),
                below:  ref GetNeighbor(x, y, z, belowOffset.X,  belowOffset.Y,  belowOffset.Z)
            );
        }

        private ref Voxel GetNeighbor(int x, int y, int z, int offsetX, int offsetY, int offsetZ)
        {
            int nX = x + offsetX;
            int nY = y + offsetY;
            int nZ = z + offsetZ;

            if (nX < 0 || nY < 0 || nZ < 0 || nX >= _voxelObject._chunkSize || nY >= _voxelObject._chunkSize || nZ >= _voxelObject._chunkSize)
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
    
    private enum Neighbor
    {
        Left = 0,
        Right = 1,
        Ahead = 2,
        Behind = 3,
        Above = 4,
        Below = 5,
    }
}
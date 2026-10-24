using System;
using WaywardBeyond.Data;

namespace WaywardBeyond.Voxels;

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

    /// <summary>Returns a copy of this chunk that the caller owns.</summary>
    public ChunkInfo ToChunkInfo()
    {
        Chunk source = Data;
        Voxel[] voxels = source.Voxels;
        var copy = new Voxel[voxels.Length];
        Array.Copy(voxels, copy, voxels.Length);

        return new ChunkInfo(Coords.X, Coords.Y, Coords.Z, new Chunk(source.Size, copy));
    }

    public Enumerator GetEnumerator()
    {
        return new Enumerator(Data.Voxels);
    }
    
    public ref struct Enumerator(Voxel[] voxels)
    {
        public ref Voxel Current => ref _voxels[_index];

        private readonly Voxel[] _voxels = voxels;
        private int _index = -1;

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
    
    public ref struct SampleEnumerator(ChunkData chunkData, VoxelObject voxelObject)
    {
        public VoxelSample Current => GetCurrentSample();

        private readonly Int3 _chunkPos = new(chunkData.Coords.X, chunkData.Coords.Y, chunkData.Coords.Z);
        private readonly Int3 _chunkWorldCoords = new(chunkData.Coords.X * chunkData.Data.Size, chunkData.Coords.Y * chunkData.Data.Size, chunkData.Coords.Z * chunkData.Data.Size);
        private readonly Voxel[] _currentVoxels = chunkData.Data.Voxels;
        private int _voxelIndex = -1;

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
            int x = _voxelIndex & ((1 << voxelObject._chunkShift) - 1);
            int y = (_voxelIndex >> voxelObject._chunkShift) & ((1 << (voxelObject._chunkShift2 - voxelObject._chunkShift)) - 1);
            int z = _voxelIndex >> voxelObject._chunkShift2;

            ref Voxel center = ref _currentVoxels[_voxelIndex];

            //  Orientation arrives from disk and the wire as a raw byte, so mask it to the 64 stored values.
            int orientation = center.Orientation & 0x3F;

            Int3 leftOffset = _neighborOffsetLookup[orientation][(int)Neighbor.Left];
            Int3 rightOffset = _neighborOffsetLookup[orientation][(int)Neighbor.Right];
            Int3 aheadOffset = _neighborOffsetLookup[orientation][(int)Neighbor.Ahead];
            Int3 behindOffset = _neighborOffsetLookup[orientation][(int)Neighbor.Behind];
            Int3 aboveOffset = _neighborOffsetLookup[orientation][(int)Neighbor.Above];
            Int3 belowOffset = _neighborOffsetLookup[orientation][(int)Neighbor.Below];

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

            if (nX < 0 || nY < 0 || nZ < 0 || nX >= voxelObject._chunkSize || nY >= voxelObject._chunkSize || nZ >= voxelObject._chunkSize)
            {
                return ref voxelObject.GetUnsafe(nX + _chunkWorldCoords.X, nY + _chunkWorldCoords.Y, nZ + _chunkWorldCoords.Z);
            }

            int neighborIndex = nX + (nY << voxelObject._chunkShift) + (nZ << voxelObject._chunkShift2);
            if (neighborIndex < 0 || neighborIndex >= voxelObject._voxelsPerChunk)
            {
                return ref voxelObject.GetUnsafe(nX + _chunkWorldCoords.X, nY + _chunkWorldCoords.Y, nZ + _chunkWorldCoords.Z);
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

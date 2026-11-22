using System;
using System.Collections.Generic;
using System.Numerics;
using WaywardBeyond.Data;

namespace WaywardBeyond.Voxels;

public readonly struct ChunkData
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

    public readonly Short3 Coords;
    public readonly Chunk Data;
    public readonly VoxelPalette Palette;

    private readonly VoxelObject _voxelObject;
    private readonly Occupancy _occupancy;
    private readonly FaceOverrideStore _faceOverrides;

    public ChunkData(in Short3 coords, in Chunk data, in VoxelObject voxelObject, in VoxelPalette? palette = null)
    {
        Coords = coords;
        Data = data;
        Palette = palette ?? new VoxelPalette(data.Voxels.Length);
        _voxelObject = voxelObject;
        _occupancy = new Occupancy();
        _faceOverrides = new FaceOverrideStore();

        if (data.FaceOverrides != null && data.FaceOverrides.Length > 0)
        {
            var map = new Dictionary<int, FaceOverride>(data.FaceOverrides.Length);
            Voxel[] voxels = data.Voxels;
            for (var i = 0; i < data.FaceOverrides.Length; i++)
            {
                int voxelIndex = data.FaceOverrides[i].Key >> 3;
                if ((uint)voxelIndex < (uint)voxels.Length)
                {
                    voxels[voxelIndex].OrientationFlags = new OrientationFlags(voxels[voxelIndex].OrientationFlags).WithFaceOverrides(false);
                }
            }

            for (var i = 0; i < data.FaceOverrides.Length; i++)
            {
                FaceOverrideData entry = data.FaceOverrides[i];
                var faceOverride = new FaceOverride(entry.TexturePaintId, entry.ColorPaintId);
                if (faceOverride.IsEmpty)
                {
                    continue;
                }

                int voxelIndex = entry.Key >> 3;
                int face = entry.Key & 7;
                if ((uint)voxelIndex >= (uint)voxels.Length || face > (int)BrickFace.Right || voxels[voxelIndex].ID == 0)
                {
                    continue;
                }

                map[entry.Key] = faceOverride;
                voxels[voxelIndex].OrientationFlags = new OrientationFlags(voxels[voxelIndex].OrientationFlags).WithFaceOverrides(true);
            }

            if (map.Count > 0)
            {
                _faceOverrides.Overrides = map;
            }
        }
    }
    
    /// <summary>Returns the rotated world-space neighbor offset for the given orientation and neighbor index (0..5: Left, Right, Ahead, Behind, Above, Below).</summary>
    public static Int3 GetRotatedNeighborOffset(Orientation orientation, int neighborIndex)
    {
        return _neighborOffsetLookup[orientation.ToByte()][neighborIndex];
    }

    /// <summary>True when at least one face in this chunk carries a paint override.</summary>
    public bool HasAnyFaceOverrides => _faceOverrides.Overrides is { Count: > 0 };

    /// <summary>Computes the chunk-local sparse key for a voxel index and brick-local face.</summary>
    public static int GetFaceKey(int voxelIndex, BrickFace face) => (voxelIndex << 3) | (int)face;

    /// <summary>Tries to read the paint override for a voxel index and brick-local face.</summary>
    public bool TryGetFaceOverride(int voxelIndex, BrickFace face, out FaceOverride faceOverride)
    {
        Dictionary<int, FaceOverride>? map = _faceOverrides.Overrides;
        if (map == null)
        {
            faceOverride = default;
            return false;
        }

        return map.TryGetValue(GetFaceKey(voxelIndex, face), out faceOverride);
    }

    /// <summary>Sets or clears the full <see cref="FaceOverride"/> on all faces in <paramref name="faceMask"/>.</summary>
    public void SetFaceOverride(int voxelIndex, BrickFaceMask faceMask, FaceOverride faceOverride)
    {
        if (faceMask == BrickFaceMask.None || Data.Voxels[voxelIndex].ID == 0)
        {
            return;
        }

        if (faceOverride.IsEmpty)
        {
            Dictionary<int, FaceOverride>? existing = _faceOverrides.Overrides;
            if (existing == null)
            {
                return;
            }

            for (var f = 0; f < 6; f++)
            {
                var face = (BrickFace)f;
                if (faceMask.HasFace(face))
                {
                    existing.Remove(GetFaceKey(voxelIndex, face));
                }
            }

            SyncVoxelOverrideFlag(voxelIndex);
            if (existing.Count == 0)
            {
                _faceOverrides.Overrides = null;
            }

            return;
        }

        Dictionary<int, FaceOverride> map = _faceOverrides.Overrides ??= [];
        for (var f = 0; f < 6; f++)
        {
            var face = (BrickFace)f;
            if (faceMask.HasFace(face))
            {
                map[GetFaceKey(voxelIndex, face)] = faceOverride;
            }
        }

        Data.Voxels[voxelIndex].OrientationFlags = new OrientationFlags(Data.Voxels[voxelIndex].OrientationFlags).WithFaceOverrides(true);
    }

    /// <summary>Sets or clears only the texture paint slot on all faces in <paramref name="faceMask"/>, preserving color tint.</summary>
    public void SetFaceTexture(int voxelIndex, BrickFaceMask faceMask, ushort texturePaintId)
    {
        if (faceMask == BrickFaceMask.None || Data.Voxels[voxelIndex].ID == 0)
        {
            return;
        }

        for (var f = 0; f < 6; f++)
        {
            var face = (BrickFace)f;
            if (!faceMask.HasFace(face))
            {
                continue;
            }

            TryGetFaceOverride(voxelIndex, face, out FaceOverride current);
            SetSingleFaceOverride(voxelIndex, face, current.WithTexture(texturePaintId));
        }

        SyncVoxelOverrideFlag(voxelIndex);
    }

    /// <summary>Sets or clears only the color tint slot on all faces in <paramref name="faceMask"/>, preserving texture paint.</summary>
    public void SetFaceColor(int voxelIndex, BrickFaceMask faceMask, ushort colorPaintId)
    {
        if (faceMask == BrickFaceMask.None || Data.Voxels[voxelIndex].ID == 0)
        {
            return;
        }

        for (var f = 0; f < 6; f++)
        {
            var face = (BrickFace)f;
            if (!faceMask.HasFace(face))
            {
                continue;
            }

            TryGetFaceOverride(voxelIndex, face, out FaceOverride current);
            SetSingleFaceOverride(voxelIndex, face, current.WithColor(colorPaintId));
        }

        SyncVoxelOverrideFlag(voxelIndex);
    }

    /// <summary>Removes all face overrides belonging to <paramref name="voxelIndex"/>.</summary>
    public void ClearFaceOverrides(int voxelIndex)
    {
        Dictionary<int, FaceOverride>? map = _faceOverrides.Overrides;
        if (map == null)
        {
            return;
        }

        for (var f = 0; f < 6; f++)
        {
            map.Remove(GetFaceKey(voxelIndex, (BrickFace)f));
        }

        if (map.Count == 0)
        {
            _faceOverrides.Overrides = null;
        }

        Data.Voxels[voxelIndex].OrientationFlags = new OrientationFlags(Data.Voxels[voxelIndex].OrientationFlags).WithFaceOverrides(false);
    }

    private void SetSingleFaceOverride(int voxelIndex, BrickFace face, FaceOverride value)
    {
        int key = GetFaceKey(voxelIndex, face);
        if (value.IsEmpty)
        {
            Dictionary<int, FaceOverride>? existing = _faceOverrides.Overrides;
            if (existing != null)
            {
                existing.Remove(key);
                if (existing.Count == 0)
                {
                    _faceOverrides.Overrides = null;
                }
            }

            return;
        }

        Dictionary<int, FaceOverride> map = _faceOverrides.Overrides ??= [];
        map[key] = value;
    }

    private void SyncVoxelOverrideFlag(int voxelIndex)
    {
        bool any = false;
        Dictionary<int, FaceOverride>? map = _faceOverrides.Overrides;
        if (map != null)
        {
            for (var f = 0; f < 6; f++)
            {
                if (map.ContainsKey(GetFaceKey(voxelIndex, (BrickFace)f)))
                {
                    any = true;
                    break;
                }
            }
        }

        Data.Voxels[voxelIndex].OrientationFlags = new OrientationFlags(Data.Voxels[voxelIndex].OrientationFlags).WithFaceOverrides(any);
    }

    /// <summary>Returns a copy of this chunk that the caller owns.</summary>
    public ChunkInfo ToChunkInfo()
    {
        Chunk source = Data;
        Voxel[] voxels = source.Voxels;
        var copy = new Voxel[voxels.Length];
        Array.Copy(voxels, copy, voxels.Length);

        FaceOverrideData[]? overridesCopy = null;
        Dictionary<int, FaceOverride>? map = _faceOverrides.Overrides;
        if (map != null && map.Count > 0)
        {
            overridesCopy = new FaceOverrideData[map.Count];
            var i = 0;
            foreach (KeyValuePair<int, FaceOverride> kvp in map)
            {
                overridesCopy[i++] = new FaceOverrideData(kvp.Key, kvp.Value.TexturePaintId, kvp.Value.ColorPaintId);
            }
        }

        return new ChunkInfo(Coords.X, Coords.Y, Coords.Z, new Chunk(source.Size, copy, overridesCopy));
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
        return new Sampler(this);
    }

    /// <summary>Returns a sampler that visits only non-empty voxels.</summary>
    public OccupiedSampler GetOccupiedSampler()
    {
        return new OccupiedSampler(this);
    }

    public readonly ref struct Sampler(ChunkData chunkData)
    {
        public SampleEnumerator GetEnumerator()
        {
            return new SampleEnumerator(chunkData);
        }
    }

    public readonly ref struct OccupiedSampler(ChunkData chunkData)
    {
        public OccupiedEnumerator GetEnumerator()
        {
            ulong[] mask = chunkData._occupancy.Mask ??= BuildMask(chunkData.Data.Voxels);
            return new OccupiedEnumerator(chunkData, mask);
        }
    }
    
    public ref struct SampleEnumerator(ChunkData chunkData)
    {
        public VoxelSample Current => _chunkData.SampleAt(_voxelIndex);

        private readonly ChunkData _chunkData = chunkData;
        private readonly int _count = chunkData.Data.Voxels.Length;
        private int _voxelIndex = -1;

        public bool MoveNext()
        {
            if (_voxelIndex + 1 >= _count)
            {
                return false;
            }

            _voxelIndex++;
            return true;
        }
    }

    public ref struct OccupiedEnumerator(ChunkData chunkData, ulong[] mask)
    {
        public VoxelSample Current => _chunkData.SampleAt(_voxelIndex);

        private readonly ChunkData _chunkData = chunkData;
        private readonly ulong[] _mask = mask;
        private int _wordIndex = -1;
        private ulong _word;
        private int _voxelIndex = -1;

        public bool MoveNext()
        {
            while (true)
            {
                if (_word != 0)
                {
                    int bit = BitOperations.TrailingZeroCount(_word);
                    _word &= _word - 1;
                    _voxelIndex = (_wordIndex << 6) + bit;
                    return true;
                }

                _wordIndex++;
                if (_wordIndex >= _mask.Length)
                {
                    return false;
                }

                _word = _mask[_wordIndex];
            }
        }
    }

    internal VoxelSample SampleAt(int voxelIndex)
    {
        int shift = _voxelObject._chunkShift;
        int shift2 = _voxelObject._chunkShift2;
        int size = _voxelObject._chunkSize;
        int mask = size - 1;

        int x = voxelIndex & mask;
        int y = (voxelIndex >> shift) & mask;
        int z = voxelIndex >> shift2;

        int worldX = Coords.X * size;
        int worldY = Coords.Y * size;
        int worldZ = Coords.Z * size;

        ref Voxel center = ref Data.Voxels[voxelIndex];

        //  Orientation arrives from disk and the wire as a raw byte, so mask it to the 64 stored values.
        int orientation = center.GetOrientation().ToByte();

        Int3 leftOffset = _neighborOffsetLookup[orientation][(int)Neighbor.Left];
        Int3 rightOffset = _neighborOffsetLookup[orientation][(int)Neighbor.Right];
        Int3 aheadOffset = _neighborOffsetLookup[orientation][(int)Neighbor.Ahead];
        Int3 behindOffset = _neighborOffsetLookup[orientation][(int)Neighbor.Behind];
        Int3 aboveOffset = _neighborOffsetLookup[orientation][(int)Neighbor.Above];
        Int3 belowOffset = _neighborOffsetLookup[orientation][(int)Neighbor.Below];

        return new VoxelSample(
            chunkOffset: new Int3(worldX, worldY, worldZ),
            chunkCoords: new Int3(Coords.X, Coords.Y, Coords.Z),
            coords: new Int3(x, y, z),
            center: ref center,
            left:   ref GetNeighbor(x, y, z, worldX, worldY, worldZ, leftOffset.X,   leftOffset.Y,   leftOffset.Z),
            right:  ref GetNeighbor(x, y, z, worldX, worldY, worldZ, rightOffset.X,  rightOffset.Y,  rightOffset.Z),
            ahead:  ref GetNeighbor(x, y, z, worldX, worldY, worldZ, aheadOffset.X,  aheadOffset.Y,  aheadOffset.Z),
            behind: ref GetNeighbor(x, y, z, worldX, worldY, worldZ, behindOffset.X, behindOffset.Y, behindOffset.Z),
            above:  ref GetNeighbor(x, y, z, worldX, worldY, worldZ, aboveOffset.X,  aboveOffset.Y,  aboveOffset.Z),
            below:  ref GetNeighbor(x, y, z, worldX, worldY, worldZ, belowOffset.X,  belowOffset.Y,  belowOffset.Z),
            chunkData: this,
            voxelObject: _voxelObject,
            voxelIndex: voxelIndex
        );
    }

    private ref Voxel GetNeighbor(int x, int y, int z, int worldX, int worldY, int worldZ, int offsetX, int offsetY, int offsetZ)
    {
        int nX = x + offsetX;
        int nY = y + offsetY;
        int nZ = z + offsetZ;
        int size = _voxelObject._chunkSize;

        if (nX < 0 || nY < 0 || nZ < 0 || nX >= size || nY >= size || nZ >= size)
        {
            return ref _voxelObject.GetUnsafe(nX + worldX, nY + worldY, nZ + worldZ);
        }

        int neighborIndex = nX + (nY << _voxelObject._chunkShift) + (nZ << _voxelObject._chunkShift2);
        return ref Data.Voxels[neighborIndex];
    }

    internal void SetOccupied(int voxelIndex, bool occupied)
    {
        ulong[]? mask = _occupancy.Mask;
        if (mask == null)
        {
            return;
        }

        int wordIndex = voxelIndex >> 6;
        ulong bit = 1UL << (voxelIndex & 63);
        if (occupied)
        {
            mask[wordIndex] |= bit;
        }
        else
        {
            mask[wordIndex] &= ~bit;
        }
    }

    private static ulong[] BuildMask(Voxel[] voxels)
    {
        var mask = new ulong[(voxels.Length + 63) >> 6];
        for (var i = 0; i < voxels.Length; i++)
        {
            if (voxels[i].ID != 0)
            {
                mask[i >> 6] |= 1UL << (i & 63);
            }
        }

        return mask;
    }

    private sealed class Occupancy
    {
        public ulong[]? Mask;
    }

    private sealed class FaceOverrideStore
    {
        public Dictionary<int, FaceOverride>? Overrides;
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

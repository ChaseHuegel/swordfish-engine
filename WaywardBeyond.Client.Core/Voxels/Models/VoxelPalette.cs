using System.Collections.Generic;

namespace WaywardBeyond.Client.Core.Voxels.Models;

public readonly struct VoxelPalette()
{
    private readonly Dictionary<ushort, int> _voxelCounts = [];

    public VoxelPalette(int zeroCount) : this()
    {
        _voxelCounts.Add(0, zeroCount);
    }

    public void Increment(ushort id)
    {
        lock (_voxelCounts)
        {
            _voxelCounts.TryGetValue(id, out int value);
            _voxelCounts[id] = value + 1;
        }
    }
    
    public void Decrement(ushort id)
    {
        lock (_voxelCounts)
        {
            if (_voxelCounts.TryGetValue(id, out int value) && value <= 1)
            {
                _voxelCounts.Remove(id);
            }
            else
            {
                _voxelCounts[id] = value - 1;
            }
        }
    }

    public void Clear()
    {
        lock (_voxelCounts)
        {
            _voxelCounts.Clear();
        }
    }
}
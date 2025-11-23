using System.Collections.Generic;
using WaywardBeyond.Client.Core.Numerics;

namespace WaywardBeyond.Client.Core.Voxels.Processing;

internal sealed class DepthState
{
    public readonly Dictionary<Int2, Int2> XY = new();
    public readonly Dictionary<Int2, Int2> XZ = new();
    public readonly Dictionary<Int2, Int2> ZY = new();
}
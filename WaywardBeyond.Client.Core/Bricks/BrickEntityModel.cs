using System.Numerics;
using Swordfish.Bricks;

namespace WaywardBeyond.Client.Core.Bricks;

internal struct BrickEntityModel(in Vector3 position, in Quaternion orientation, in BrickGrid grid)
{
    public Vector3 Position = position;
    public Quaternion Orientation = orientation;
    public BrickGrid Grid = grid;
}
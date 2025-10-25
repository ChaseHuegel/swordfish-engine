using System;
using System.Numerics;
using Swordfish.Bricks;

namespace WaywardBeyond.Client.Core.Bricks;

internal struct BrickEntityModel(in Guid guid, in Vector3 position, in Quaternion orientation, in BrickGrid grid)
{
    public Guid Guid = guid;
    public Vector3 Position = position;
    public Quaternion Orientation = orientation;
    public BrickGrid Grid = grid;
}
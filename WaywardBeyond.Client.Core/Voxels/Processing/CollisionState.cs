using System.Collections.Generic;
using System.Numerics;
using Swordfish.Library.Types.Shapes;

namespace WaywardBeyond.Client.Core.Voxels.Processing;

internal sealed class CollisionState
{
    public readonly List<Shape> Shapes = [];
    public readonly List<Vector3> Positions = [];
    public readonly List<Quaternion> Orientations = [];
}
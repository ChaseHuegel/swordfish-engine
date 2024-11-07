using System.Numerics;

namespace Swordfish.Library.Types.Shapes;

public readonly struct CompoundShape(in Shape[] shapes, in Vector3[] positions, in Quaternion[] orientations)
{
    public readonly Shape[] Shapes = shapes;
    public readonly Vector3[] Positions = positions;
    public readonly Quaternion[] Orientations = orientations;
}
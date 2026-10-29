using System.Numerics;
using Swordfish.Library.Types.Shapes;

namespace WaywardBeyond.Client.Voxels;

/// <summary>Stores the cached surface collision boxes and transforms for a single chunk.</summary>
public readonly struct ChunkCollisionData(in Shape[] shapes, in Vector3[] positions, in Quaternion[] orientations)
{
    public readonly Shape[] Shapes = shapes;
    public readonly Vector3[] Positions = positions;
    public readonly Quaternion[] Orientations = orientations;
    public readonly bool IsEmpty => Shapes == null || Shapes.Length == 0;
}

using System.Numerics;
using Swordfish.Library.Types.Shapes;

namespace WaywardBeyond.Server.Components;

/// <summary>Stores the cached collision primitives and transforms for one server voxel chunk.</summary>
public readonly struct ChunkColliderData(Shape[] shapes, Vector3[] positions, Quaternion[] orientations)
{
    /// <summary>The merged or shaped collision primitives in this chunk.</summary>
    public readonly Shape[] Shapes = shapes;

    /// <summary>The structure-local position for each primitive.</summary>
    public readonly Vector3[] Positions = positions;

    /// <summary>The structure-local orientation for each primitive.</summary>
    public readonly Quaternion[] Orientations = orientations;

    /// <summary>True when this chunk holds no collision primitives.</summary>
    public bool IsEmpty => Shapes == null || Shapes.Length == 0;
}

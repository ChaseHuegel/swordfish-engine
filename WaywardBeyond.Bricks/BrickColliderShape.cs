using System.Numerics;
using Swordfish.Library.Types.Shapes;

namespace WaywardBeyond.Bricks;

/// <summary>One collision primitive in brick-local space, centered on the voxel origin.</summary>
public readonly record struct BrickColliderShape(Shape Shape, Vector3 Offset, Quaternion Orientation);

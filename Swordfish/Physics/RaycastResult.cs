using System.Numerics;
using Swordfish.ECS;

namespace Swordfish.Physics;

public readonly struct RaycastResult(bool hit, Entity entity, Vector3 point, Vector3 normal)
{
    public readonly bool Hit = hit;
    public readonly Entity Entity = entity;
    public readonly Vector3 Point = point;
    public readonly Vector3 Normal = normal;
}
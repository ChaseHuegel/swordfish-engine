using System.Numerics;

namespace Swordfish.ECS;

public struct ChildComponent(in int parent) : IDataComponent
{
    public int Parent = parent;
    public Vector3 LocalPosition;
    public Quaternion LocalOrientation;
    public Vector3 LocalScale = Vector3.One;
}
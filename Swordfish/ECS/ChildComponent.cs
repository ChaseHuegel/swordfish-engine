using System.Numerics;

namespace Swordfish.ECS;

public struct ChildComponent(in Uuid parent) : IDataComponent
{
    public Uuid Parent = parent;
    public Vector3 LocalPosition;
    public Quaternion LocalOrientation = Quaternion.Identity;
    public Vector3 LocalScale = Vector3.One;
}
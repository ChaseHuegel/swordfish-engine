using System.Numerics;

namespace Swordfish.Library.Types.Shapes;

public struct Plane(in Vector3 normal, in float distance = 0f)
{
    public Vector3 Normal = normal;
    public float Distance = distance;

    public Vector3 GetPosition()
    {
        return Normal * Distance;
    }

    public static implicit operator Shape(Plane x) => new(x);
}
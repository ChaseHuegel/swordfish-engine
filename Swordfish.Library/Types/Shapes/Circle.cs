namespace Swordfish.Library.Types.Shapes;

public struct Circle(in float radius)
{
    public float Radius = radius;

    public static implicit operator Shape(Circle x) => new(x);
}
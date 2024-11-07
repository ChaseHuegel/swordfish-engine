namespace Swordfish.Library.Types.Shapes;

public struct Sphere(in float radius)
{
    public float Radius = radius;

    public static implicit operator Shape(Sphere x) => new(x);
}
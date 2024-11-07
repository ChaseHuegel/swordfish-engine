namespace Swordfish.Library.Types.Shapes;

public struct Cylinder(in float height, in float radius)
{
    public float Height = height;
    public float Radius = radius;

    public static implicit operator Shape(Cylinder x) => new(x);
}
namespace Swordfish.Library.Types.Shapes;

public struct Capsule(in float height, in float radius)
{
    public float Height = height;
    public float Radius = radius;

    public static implicit operator Shape(Capsule x) => new(x);
}
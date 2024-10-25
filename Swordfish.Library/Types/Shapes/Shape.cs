using System.Runtime.InteropServices;

namespace Swordfish.Library.Types.Shapes;

[StructLayout(LayoutKind.Explicit)]
public readonly struct Shape
{
    [FieldOffset(0)]
    public readonly ShapeType Type;

    [FieldOffset(4)]
    public readonly Box2 Box2;

    [FieldOffset(4)]
    public readonly Box3 Box3;

    [FieldOffset(4)]
    public readonly Capsule Capsule;

    [FieldOffset(4)]
    public readonly Circle Circle;

    [FieldOffset(4)]
    public readonly Cylinder Cylinder;

    [FieldOffset(4)]
    public readonly Ellipse2 Ellipse2;

    [FieldOffset(4)]
    public readonly Plane Plane;

    [FieldOffset(4)]
    public readonly Sphere Sphere;

    public Shape(Box2 box2)
    {
        Type = ShapeType.Box2;
        Box2 = box2;
    }

    public Shape(Box3 box3)
    {
        Type = ShapeType.Box3;
        Box3 = box3;
    }

    public Shape(Capsule capsule)
    {
        Type = ShapeType.Capsule;
        Capsule = capsule;
    }

    public Shape(Circle circle)
    {
        Type = ShapeType.Circle;
        Circle = circle;
    }

    public Shape(Cylinder cylinder)
    {
        Type = ShapeType.Cylinder;
        Cylinder = cylinder;
    }

    public Shape(Ellipse2 ellipse2)
    {
        Type = ShapeType.Ellipse2;
        Ellipse2 = ellipse2;
    }

    public Shape(Plane plane)
    {
        Type = ShapeType.Plane;
        Plane = plane;
    }

    public Shape(Sphere sphere)
    {
        Type = ShapeType.Sphere;
        Sphere = sphere;
    }
}
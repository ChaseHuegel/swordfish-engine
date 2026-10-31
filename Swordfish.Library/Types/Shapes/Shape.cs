using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Swordfish.Library.Types.Shapes;

namespace Swordfish.Library.Types.Shapes;

[StructLayout(LayoutKind.Explicit)]
public readonly struct Shape : IEquatable<Shape>
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

    /// <inheritdoc/>
    public bool Equals(Shape other)
    {
        if (Type != other.Type)
        {
            return false;
        }

        return Type switch
        {
            ShapeType.Box3 => QuantizedEquals(Box3.Extents, other.Box3.Extents),
            
            ShapeType.Box2 => QuantizedEquals(Box2.Extents, other.Box2.Extents),
            
            ShapeType.Sphere => Quantize(Sphere.Radius) == Quantize(other.Sphere.Radius),
            
            ShapeType.Circle => Quantize(Circle.Radius) == Quantize(other.Circle.Radius),
            
            ShapeType.Capsule => Quantize(Capsule.Height) == Quantize(other.Capsule.Height) &&
                                 Quantize(Capsule.Radius) == Quantize(other.Capsule.Radius),
            
            ShapeType.Cylinder => Quantize(Cylinder.Height) == Quantize(other.Cylinder.Height) &&
                                  Quantize(Cylinder.Radius) == Quantize(other.Cylinder.Radius),
            
            ShapeType.Plane => QuantizedEquals(Plane.Normal, other.Plane.Normal) &&
                               Quantize(Plane.Distance) == Quantize(other.Plane.Distance),
            
            ShapeType.Ellipse2 => Quantize(Ellipse2.A) == Quantize(other.Ellipse2.A) &&
                                  Quantize(Ellipse2.B) == Quantize(other.Ellipse2.B) &&
                                  Quantize(Ellipse2.H) == Quantize(other.Ellipse2.H) &&
                                  Quantize(Ellipse2.K) == Quantize(other.Ellipse2.K) &&
                                  Quantize(Ellipse2.Theta) == Quantize(other.Ellipse2.Theta),
            _ => false,
        };
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Shape other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return Type switch
        {
            ShapeType.Box3 => HashCode.Combine(Type, Quantize(Box3.Extents.X), Quantize(Box3.Extents.Y), Quantize(Box3.Extents.Z)),
            ShapeType.Box2 => HashCode.Combine(Type, Quantize(Box2.Extents.X), Quantize(Box2.Extents.Y)),
            ShapeType.Sphere => HashCode.Combine(Type, Quantize(Sphere.Radius)),
            ShapeType.Circle => HashCode.Combine(Type, Quantize(Circle.Radius)),
            ShapeType.Capsule => HashCode.Combine(Type, Quantize(Capsule.Height), Quantize(Capsule.Radius)),
            ShapeType.Cylinder => HashCode.Combine(Type, Quantize(Cylinder.Height), Quantize(Cylinder.Radius)),
            ShapeType.Plane => HashCode.Combine(
                Type,
                Quantize(Plane.Normal.X),
                Quantize(Plane.Normal.Y),
                Quantize(Plane.Normal.Z),
                Quantize(Plane.Distance)
            ),
            ShapeType.Ellipse2 => HashCode.Combine(
                Type,
                Quantize(Ellipse2.A),
                Quantize(Ellipse2.B),
                Quantize(Ellipse2.H),
                Quantize(Ellipse2.K),
                Quantize(Ellipse2.Theta)
            ),
            _ => (int)Type,
        };
    }

    private const float QUANTIZATION_STEP = 0.0001f;

    private static int Quantize(float value) => (int)MathF.Round(value / QUANTIZATION_STEP);

    private static bool QuantizedEquals(Vector3 left, Vector3 right)
    {
        return Quantize(left.X) == Quantize(right.X)
            && Quantize(left.Y) == Quantize(right.Y)
            && Quantize(left.Z) == Quantize(right.Z);
    }

    private static bool QuantizedEquals(Vector2 left, Vector2 right)
    {
        return Quantize(left.X) == Quantize(right.X)
            && Quantize(left.Y) == Quantize(right.Y);
    }

    /// <summary>Returns true if two shapes are equal.</summary>
    public static bool operator ==(Shape left, Shape right) => left.Equals(right);

    /// <summary>Returns true if two shapes are not equal.</summary>
    public static bool operator !=(Shape left, Shape right) => !left.Equals(right);
}
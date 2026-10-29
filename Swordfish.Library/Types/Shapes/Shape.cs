using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Swordfish.Library.Extensions;

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

        const float tolerance = 0.00001f;
        return Type switch
        {
            ShapeType.Box3 => Box3.Extents == other.Box3.Extents,
            
            ShapeType.Box2 => Box2.Extents == other.Box2.Extents,
            
            ShapeType.Sphere => Math.Abs(Sphere.Radius - other.Sphere.Radius) < tolerance,
            
            ShapeType.Circle => Math.Abs(Circle.Radius - other.Circle.Radius) < tolerance,
            
            ShapeType.Capsule => Math.Abs(Capsule.Height - other.Capsule.Height) < tolerance &&
                                 Math.Abs(Capsule.Radius - other.Capsule.Radius) < tolerance,
            
            ShapeType.Cylinder => Math.Abs(Cylinder.Height - other.Cylinder.Height) < tolerance &&
                                  Math.Abs(Cylinder.Radius - other.Cylinder.Radius) < tolerance,
            
            ShapeType.Plane => Plane.Normal.ApproximatelyEquals(other.Plane.Normal) &&
                               Math.Abs(Plane.Distance - other.Plane.Distance) < tolerance,
            
            ShapeType.Ellipse2 => Math.Abs(Ellipse2.A - other.Ellipse2.A) < tolerance &&
                                  Math.Abs(Ellipse2.B - other.Ellipse2.B) < tolerance &&
                                  Math.Abs(Ellipse2.H - other.Ellipse2.H) < tolerance &&
                                  Math.Abs(Ellipse2.K - other.Ellipse2.K) < tolerance &&
                                  Math.Abs(Ellipse2.Theta - other.Ellipse2.Theta) < tolerance,
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
            ShapeType.Box3 => HashCode.Combine(Type, Box3.Extents),
            ShapeType.Box2 => HashCode.Combine(Type, Box2.Extents),
            ShapeType.Sphere => HashCode.Combine(Type, Sphere.Radius),
            ShapeType.Circle => HashCode.Combine(Type, Circle.Radius),
            ShapeType.Capsule => HashCode.Combine(Type, Capsule.Height, Capsule.Radius),
            ShapeType.Cylinder => HashCode.Combine(Type, Cylinder.Height, Cylinder.Radius),
            ShapeType.Plane => HashCode.Combine(Type, Plane.Normal, Plane.Distance),
            ShapeType.Ellipse2 => HashCode.Combine(Type, Ellipse2.A, Ellipse2.B, Ellipse2.H, Ellipse2.K, Ellipse2.Theta),
            _ => (int)Type,
        };
    }

    /// <summary>Returns true if two shapes are equal.</summary>
    public static bool operator ==(Shape left, Shape right) => left.Equals(right);

    /// <summary>Returns true if two shapes are not equal.</summary>
    public static bool operator !=(Shape left, Shape right) => !left.Equals(right);
}
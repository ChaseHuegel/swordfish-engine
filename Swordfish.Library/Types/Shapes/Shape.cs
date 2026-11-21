#nullable enable
using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Swordfish.Library.Types.Shapes;

/// <summary>A tagged collision or geometry shape.</summary>
public readonly struct Shape : IEquatable<Shape>
{
    public readonly ShapeType Type;
    private readonly PrimitiveUnion _primitive;
    public readonly ConvexHull? ConvexHull;

    public Box2 Box2 => _primitive.Box2;
    public Box3 Box3 => _primitive.Box3;
    public Capsule Capsule => _primitive.Capsule;
    public Circle Circle => _primitive.Circle;
    public Cylinder Cylinder => _primitive.Cylinder;
    public Ellipse2 Ellipse2 => _primitive.Ellipse2;
    public Plane Plane => _primitive.Plane;
    public Sphere Sphere => _primitive.Sphere;

    public Shape(Box2 box2)
    {
        Type = ShapeType.Box2;
        _primitive = new PrimitiveUnion(box2);
        ConvexHull = null;
    }

    public Shape(Box3 box3)
    {
        Type = ShapeType.Box3;
        _primitive = new PrimitiveUnion(box3);
        ConvexHull = null;
    }

    public Shape(Capsule capsule)
    {
        Type = ShapeType.Capsule;
        _primitive = new PrimitiveUnion(capsule);
        ConvexHull = null;
    }

    public Shape(Circle circle)
    {
        Type = ShapeType.Circle;
        _primitive = new PrimitiveUnion(circle);
        ConvexHull = null;
    }

    public Shape(Cylinder cylinder)
    {
        Type = ShapeType.Cylinder;
        _primitive = new PrimitiveUnion(cylinder);
        ConvexHull = null;
    }

    public Shape(Ellipse2 ellipse2)
    {
        Type = ShapeType.Ellipse2;
        _primitive = new PrimitiveUnion(ellipse2);
        ConvexHull = null;
    }

    public Shape(Plane plane)
    {
        Type = ShapeType.Plane;
        _primitive = new PrimitiveUnion(plane);
        ConvexHull = null;
    }

    public Shape(Sphere sphere)
    {
        Type = ShapeType.Sphere;
        _primitive = new PrimitiveUnion(sphere);
        ConvexHull = null;
    }

    public Shape(ConvexHull convexHull)
    {
        if (convexHull == null)
        {
            throw new ArgumentNullException(nameof(convexHull));
        }

        Type = ShapeType.ConvexHull;
        _primitive = default;
        ConvexHull = convexHull;
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

            ShapeType.ConvexHull => ConvexHull != null && ConvexHull.Equals(other.ConvexHull),

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
            ShapeType.ConvexHull => HashCode.Combine(Type, ConvexHull?.GetHashCode() ?? 0),
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

    [StructLayout(LayoutKind.Explicit)]
    private readonly struct PrimitiveUnion
    {
        [FieldOffset(0)]
        public readonly Box2 Box2;

        [FieldOffset(0)]
        public readonly Box3 Box3;

        [FieldOffset(0)]
        public readonly Capsule Capsule;

        [FieldOffset(0)]
        public readonly Circle Circle;

        [FieldOffset(0)]
        public readonly Cylinder Cylinder;

        [FieldOffset(0)]
        public readonly Ellipse2 Ellipse2;

        [FieldOffset(0)]
        public readonly Plane Plane;

        [FieldOffset(0)]
        public readonly Sphere Sphere;

        public PrimitiveUnion(Box2 box2) => Box2 = box2;
        public PrimitiveUnion(Box3 box3) => Box3 = box3;
        public PrimitiveUnion(Capsule capsule) => Capsule = capsule;
        public PrimitiveUnion(Circle circle) => Circle = circle;
        public PrimitiveUnion(Cylinder cylinder) => Cylinder = cylinder;
        public PrimitiveUnion(Ellipse2 ellipse2) => Ellipse2 = ellipse2;
        public PrimitiveUnion(Plane plane) => Plane = plane;
        public PrimitiveUnion(Sphere sphere) => Sphere = sphere;
    }
}

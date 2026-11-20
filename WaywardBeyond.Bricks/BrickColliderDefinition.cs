using System;
using System.Collections.Generic;
using System.Numerics;
using Swordfish.Library.Types.Shapes;

namespace WaywardBeyond.Bricks;

/// <summary>TOML schema for one collider primitive declared under <c>[[Bricks.Colliders]]</c>.</summary>
internal struct BrickColliderDefinition()
{
    public string? Type;
    public Float3 Size = new(1f, 1f, 1f);
    public Float3 Offset;
    public Float3 Rotation;
    public float Radius;
    public float Height;
    public List<Float3>? Points;

    /// <summary>Converts this definition into a runtime <see cref="BrickColliderShape"/>, or returns false when invalid.</summary>
    public readonly bool TryBuild(out BrickColliderShape collider, out string error)
    {
        string kind = string.IsNullOrWhiteSpace(Type) ? "box" : Type.Trim();
        Vector3 offset = Offset.ToVector3();
        Quaternion orientation = Rotation.IsZero ? Quaternion.Identity : ToQuaternion(Rotation);

        if (kind.Equals("box", StringComparison.OrdinalIgnoreCase))
        {
            Vector3 size = Size.ToVector3();
            if (size.X <= 0f || size.Y <= 0f || size.Z <= 0f)
            {
                collider = default;
                error = $"Box collider requires positive Size components, got ({size.X}, {size.Y}, {size.Z}).";
                return false;
            }

            collider = new BrickColliderShape(new Box3(size), offset, orientation);
            error = string.Empty;
            return true;
        }

        if (kind.Equals("sphere", StringComparison.OrdinalIgnoreCase))
        {
            if (Radius <= 0f)
            {
                collider = default;
                error = $"Sphere collider requires positive Radius, got {Radius}.";
                return false;
            }

            collider = new BrickColliderShape(new Sphere(Radius), offset, orientation);
            error = string.Empty;
            return true;
        }

        if (kind.Equals("capsule", StringComparison.OrdinalIgnoreCase))
        {
            if (Height <= 0f || Radius <= 0f)
            {
                collider = default;
                error = $"Capsule collider requires positive Height and Radius, got Height={Height}, Radius={Radius}.";
                return false;
            }

            collider = new BrickColliderShape(new Capsule(Height, Radius), offset, orientation);
            error = string.Empty;
            return true;
        }

        if (kind.Equals("cylinder", StringComparison.OrdinalIgnoreCase))
        {
            if (Height <= 0f || Radius <= 0f)
            {
                collider = default;
                error = $"Cylinder collider requires positive Height and Radius, got Height={Height}, Radius={Radius}.";
                return false;
            }

            collider = new BrickColliderShape(new Cylinder(Height, Radius), offset, orientation);
            error = string.Empty;
            return true;
        }

        if (kind.Equals("hull", StringComparison.OrdinalIgnoreCase))
        {
            if (Points == null || Points.Count < 4)
            {
                collider = default;
                error = $"Convex hull collider requires at least 4 Points, got {Points?.Count ?? 0}.";
                return false;
            }

            var points = new Vector3[Points.Count];
            for (var i = 0; i < Points.Count; i++)
            {
                points[i] = Points[i].ToVector3();
            }

            collider = new BrickColliderShape(new ConvexHull(points), offset, orientation);
            error = string.Empty;
            return true;
        }

        collider = default;
        error = $"Unknown collider type \"{kind}\".";
        return false;
    }

    private static Quaternion ToQuaternion(Float3 degrees)
    {
        const float DEG_TO_RAD = MathF.PI / 180f;
        return Quaternion.CreateFromYawPitchRoll(
            degrees.Y * DEG_TO_RAD,
            degrees.X * DEG_TO_RAD,
            degrees.Z * DEG_TO_RAD
        );
    }

    /// <summary>A three-component float vector for brick collider TOML tables.</summary>
    public record struct Float3()
    {
        public float X;
        public float Y;
        public float Z;

        public Float3(float x, float y, float z) : this()
        {
            X = x;
            Y = y;
            Z = z;
        }

        public readonly bool IsZero => X == 0f && Y == 0f && Z == 0f;

        public readonly Vector3 ToVector3() => new(X, Y, Z);
    }
}

using System;
using System.Collections.Generic;
using System.Numerics;
using Swordfish.Library.Types.Shapes;
using Swordfish.Library.Util;

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

    /// <summary>Builds a validated runtime <see cref="BrickColliderShape"/> from this TOML definition.</summary>
    public readonly Result<BrickColliderShape> Build()
    {
        if (!Offset.IsFinite)
        {
            return Result<BrickColliderShape>.FromFailure($"Collider Offset must be finite, got ({Offset.X}, {Offset.Y}, {Offset.Z}).");
        }

        if (!Rotation.IsFinite)
        {
            return Result<BrickColliderShape>.FromFailure($"Collider Rotation must be finite, got ({Rotation.X}, {Rotation.Y}, {Rotation.Z}).");
        }

        string kind = string.IsNullOrWhiteSpace(Type) ? "box" : Type.Trim();
        Vector3 offset = Offset.ToVector3();
        Quaternion orientation = Rotation.IsZero ? Quaternion.Identity : ToQuaternion(Rotation);

        if (kind.Equals("box", StringComparison.OrdinalIgnoreCase))
        {
            if (!Size.IsFinite || Size.X <= 0f || Size.Y <= 0f || Size.Z <= 0f)
            {
                return Result<BrickColliderShape>.FromFailure($"Box collider requires finite positive Size components, got ({Size.X}, {Size.Y}, {Size.Z}).");
            }

            return Result<BrickColliderShape>.FromSuccess(new BrickColliderShape(new Box3(Size.ToVector3()), offset, orientation));
        }

        if (kind.Equals("sphere", StringComparison.OrdinalIgnoreCase))
        {
            if (!float.IsFinite(Radius) || Radius <= 0f)
            {
                return Result<BrickColliderShape>.FromFailure($"Sphere collider requires finite positive Radius, got {Radius}.");
            }

            return Result<BrickColliderShape>.FromSuccess(new BrickColliderShape(new Sphere(Radius), offset, orientation));
        }

        if (kind.Equals("capsule", StringComparison.OrdinalIgnoreCase))
        {
            if (!float.IsFinite(Height) || !float.IsFinite(Radius) || Height <= 0f || Radius <= 0f)
            {
                return Result<BrickColliderShape>.FromFailure($"Capsule collider requires finite positive Height and Radius, got Height={Height}, Radius={Radius}.");
            }

            return Result<BrickColliderShape>.FromSuccess(new BrickColliderShape(new Capsule(Height, Radius), offset, orientation));
        }

        if (kind.Equals("cylinder", StringComparison.OrdinalIgnoreCase))
        {
            if (!float.IsFinite(Height) || !float.IsFinite(Radius) || Height <= 0f || Radius <= 0f)
            {
                return Result<BrickColliderShape>.FromFailure($"Cylinder collider requires finite positive Height and Radius, got Height={Height}, Radius={Radius}.");
            }

            return Result<BrickColliderShape>.FromSuccess(new BrickColliderShape(new Cylinder(Height, Radius), offset, orientation));
        }

        if (kind.Equals("hull", StringComparison.OrdinalIgnoreCase))
        {
            if (Points == null || Points.Count < 4)
            {
                return Result<BrickColliderShape>.FromFailure($"Convex hull collider requires at least 4 Points, got {Points?.Count ?? 0}.");
            }

            var points = new Vector3[Points.Count];
            for (var i = 0; i < Points.Count; i++)
            {
                if (!Points[i].IsFinite)
                {
                    return Result<BrickColliderShape>.FromFailure($"Convex hull point #{i} must be finite.");
                }

                points[i] = Points[i].ToVector3();
            }

            if (!HasNonZeroVolume(points))
            {
                return Result<BrickColliderShape>.FromFailure("Convex hull collider points must span a non-zero 3D volume.");
            }

            return Result<BrickColliderShape>.FromSuccess(new BrickColliderShape(new ConvexHull(points), offset, orientation));
        }

        return Result<BrickColliderShape>.FromFailure($"Unknown collider type \"{kind}\".");
    }

    private static bool HasNonZeroVolume(Vector3[] points)
    {
        const float EPSILON = 1e-6f;
        Vector3 origin = points[0];

        var secondIndex = -1;
        for (var i = 1; i < points.Length; i++)
        {
            if (Vector3.DistanceSquared(origin, points[i]) > EPSILON)
            {
                secondIndex = i;
                break;
            }
        }

        if (secondIndex < 0)
        {
            return false;
        }

        Vector3 edgeA = points[secondIndex] - origin;
        Vector3 normal = default;
        var foundPlane = false;
        for (var i = secondIndex + 1; i < points.Length; i++)
        {
            Vector3 cross = Vector3.Cross(edgeA, points[i] - origin);
            if (cross.LengthSquared() > EPSILON)
            {
                normal = cross;
                foundPlane = true;
                break;
            }
        }

        if (!foundPlane)
        {
            return false;
        }

        for (var i = 1; i < points.Length; i++)
        {
            if (MathF.Abs(Vector3.Dot(normal, points[i] - origin)) > EPSILON)
            {
                return true;
            }
        }

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

        public readonly bool IsFinite => float.IsFinite(X) && float.IsFinite(Y) && float.IsFinite(Z);

        public readonly Vector3 ToVector3() => new(X, Y, Z);
    }
}

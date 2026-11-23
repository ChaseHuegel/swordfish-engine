using System;
using System.Numerics;
using Krill;
using Swordfish.ECS;
using Swordfish.Library.Types.Shapes;
using Swordfish.Library.Util;
using WaywardBeyond.Data.Components;
using ShapePlane = Swordfish.Library.Types.Shapes.Plane;

namespace WaywardBeyond.Networking.Codecs;

/// <summary>
/// Replicates a <see cref="ColliderComponent"/> as <see cref="ColliderMessage"/>. A single shape travels
/// as a one-part compound, so the receiver always materializes a compound collider.
/// </summary>
public sealed class ColliderCodec : IPayloadCodec<ColliderComponent>
{
    private const float MIN_UNIT_LENGTH_SQ = 1e-12f;

    /// <inheritdoc/>
    public Result<byte[]> Serialize(DataStore store, int entity)
    {
        if (!store.TryGet(entity, out ColliderComponent collider))
        {
            return Result<byte[]>.FromFailure($"entity {entity} has no {nameof(ColliderComponent)} to serialize.");
        }

        if (!TryCreateMessage(in collider, out ColliderMessage message, out string error))
        {
            return Result<byte[]>.FromFailure($"entity {entity} has a collider that cannot be replicated: {error}.");
        }

        return Result<byte[]>.FromSuccess(message.Serialize());
    }

    /// <inheritdoc/>
    public Result Apply(DataStore store, int entity, ReadOnlySpan<byte> payload)
    {
        ColliderMessage message;
        try
        {
            message = ColliderMessage.Deserialize(payload);
        }
        catch (Exception ex)
        {
            return Result.FromFailure(ex);
        }

        if (message.Shapes == null)
        {
            return Result.FromFailure($"entity {entity} has a malformed collider snapshot: its shapes are missing.");
        }

        ColliderShapeMessage[] shapes = message.Shapes;
        Float3Message[] positions = message.Positions ?? [];
        Float4Message[] orientations = message.Orientations ?? [];
        if (shapes.Length != positions.Length || shapes.Length != orientations.Length)
        {
            return Result.FromFailure(
                $"entity {entity} has a malformed collider snapshot: its shape and transform counts differ.");
        }

        var parts = new Shape[shapes.Length];
        var offsets = new Vector3[positions.Length];
        var rotations = new Quaternion[orientations.Length];
        for (var index = 0; index < shapes.Length; index++)
        {
            if (!TryCreateShape(in shapes[index], out parts[index], out string error))
            {
                return Result.FromFailure($"entity {entity} has a malformed collider snapshot: {error}.");
            }

            var offset = new Vector3(positions[index].X, positions[index].Y, positions[index].Z);
            if (!IsFinite(in offset))
            {
                return Result.FromFailure($"entity {entity} has a malformed collider snapshot: a shape position is non-finite.");
            }

            var rotation = new Quaternion(
                orientations[index].X,
                orientations[index].Y,
                orientations[index].Z,
                orientations[index].W
            );
            if (!IsFinite(in rotation) || rotation.LengthSquared() < MIN_UNIT_LENGTH_SQ)
            {
                return Result.FromFailure($"entity {entity} has a malformed collider snapshot: a shape orientation is invalid.");
            }

            offsets[index] = offset;
            rotations[index] = rotation;
        }

        store.AddOrUpdate(entity, new ColliderComponent(new CompoundShape(parts, offsets, rotations)));
        return Result.FromSuccess();
    }

    /// <inheritdoc/>
    public byte[] Serialize(in ColliderComponent value)
    {
        if (!TryCreateMessage(in value, out ColliderMessage message, out string error))
        {
            throw new InvalidOperationException($"The collider cannot be replicated: {error}.");
        }

        return message.Serialize();
    }

    private static bool TryCreateMessage(
        in ColliderComponent collider,
        out ColliderMessage message,
        out string error
    ) {
        if (collider.CompoundShape.HasValue)
        {
            CompoundShape compound = collider.CompoundShape.Value;
            return TryCreateMessage(compound.Shapes, compound.Positions, compound.Orientations, out message, out error);
        }

        if (collider.Shape.HasValue)
        {
            return TryCreateMessage(
                [collider.Shape.Value],
                [Vector3.Zero],
                [Quaternion.Identity],
                out message,
                out error
            );
        }

        message = default;
        error = "the collider has no shape";
        return false;
    }

    private static bool TryCreateMessage(
        Shape[] shapes,
        Vector3[] positions,
        Quaternion[] orientations,
        out ColliderMessage message,
        out string error
    ) {
        if (shapes == null || positions == null || orientations == null)
        {
            message = default;
            error = "its shape or transform data is missing";
            return false;
        }

        if (shapes.Length != positions.Length || shapes.Length != orientations.Length)
        {
            message = default;
            error = "its shape and transform counts differ";
            return false;
        }

        message = new ColliderMessage
        {
            Shapes = new ColliderShapeMessage[shapes.Length],
            Positions = new Float3Message[shapes.Length],
            Orientations = new Float4Message[shapes.Length],
        };

        error = string.Empty;
        for (var index = 0; index < shapes.Length; index++)
        {
            if (!TryCreateMessage(in shapes[index], out message.Shapes[index], out error))
            {
                return false;
            }

            Vector3 position = positions[index];
            message.Positions[index] = new Float3Message { X = position.X, Y = position.Y, Z = position.Z };

            Quaternion orientation = orientations[index];
            message.Orientations[index] = new Float4Message
            {
                X = orientation.X,
                Y = orientation.Y,
                Z = orientation.Z,
                W = orientation.W,
            };
        }

        return true;
    }

    private static bool TryCreateMessage(in Shape shape, out ColliderShapeMessage message, out string error)
    {
        message = new ColliderShapeMessage { Type = (byte)shape.Type, Points = [] };
        error = string.Empty;

        switch (shape.Type)
        {
            case ShapeType.Box3:
                message.X = shape.Box3.Extents.X;
                message.Y = shape.Box3.Extents.Y;
                message.Z = shape.Box3.Extents.Z;
                return true;

            case ShapeType.Box2:
                message.X = shape.Box2.Extents.X;
                message.Y = shape.Box2.Extents.Y;
                return true;

            case ShapeType.Sphere:
                message.X = shape.Sphere.Radius;
                return true;

            case ShapeType.Circle:
                message.X = shape.Circle.Radius;
                return true;

            case ShapeType.Capsule:
                message.X = shape.Capsule.Height;
                message.Y = shape.Capsule.Radius;
                return true;

            case ShapeType.Cylinder:
                message.X = shape.Cylinder.Height;
                message.Y = shape.Cylinder.Radius;
                return true;

            case ShapeType.Plane:
                message.X = shape.Plane.Normal.X;
                message.Y = shape.Plane.Normal.Y;
                message.Z = shape.Plane.Normal.Z;
                message.W = shape.Plane.Distance;
                return true;

            case ShapeType.ConvexHull:
                if (shape.ConvexHull == null || shape.ConvexHull.Count < 4)
                {
                    error = "the convex hull has fewer than four points";
                    return false;
                }

                ReadOnlySpan<Vector3> points = shape.ConvexHull.Points;
                var wirePoints = new Float3Message[points.Length];
                for (var index = 0; index < points.Length; index++)
                {
                    wirePoints[index] = new Float3Message
                    {
                        X = points[index].X,
                        Y = points[index].Y,
                        Z = points[index].Z,
                    };
                }

                message.Points = wirePoints;
                return true;

            default:
                error = $"the shape type {shape.Type} is not a 3D collider";
                return false;
        }
    }

    private static bool TryCreateShape(in ColliderShapeMessage message, out Shape shape, out string error)
    {
        shape = default;
        error = string.Empty;

        switch ((ShapeType)message.Type)
        {
            case ShapeType.Box3:
                if (!IsPositiveFinite(message.X) || !IsPositiveFinite(message.Y) || !IsPositiveFinite(message.Z))
                {
                    error = "the box extents are invalid";
                    return false;
                }

                shape = new Box3(new Vector3(message.X, message.Y, message.Z));
                return true;

            case ShapeType.Box2:
                if (!IsPositiveFinite(message.X) || !IsPositiveFinite(message.Y))
                {
                    error = "the box extents are invalid";
                    return false;
                }

                shape = new Box2(new Vector2(message.X, message.Y));
                return true;

            case ShapeType.Sphere:
                if (!IsPositiveFinite(message.X))
                {
                    error = "the sphere radius is invalid";
                    return false;
                }

                shape = new Sphere(message.X);
                return true;

            case ShapeType.Circle:
                if (!IsPositiveFinite(message.X))
                {
                    error = "the circle radius is invalid";
                    return false;
                }

                shape = new Circle(message.X);
                return true;

            case ShapeType.Capsule:
                if (!IsPositiveFinite(message.X) || !IsPositiveFinite(message.Y))
                {
                    error = "the capsule dimensions are invalid";
                    return false;
                }

                shape = new Capsule(message.X, message.Y);
                return true;

            case ShapeType.Cylinder:
                if (!IsPositiveFinite(message.X) || !IsPositiveFinite(message.Y))
                {
                    error = "the cylinder dimensions are invalid";
                    return false;
                }

                shape = new Cylinder(message.X, message.Y);
                return true;

            case ShapeType.Plane:
                var normal = new Vector3(message.X, message.Y, message.Z);
                if (!IsFinite(in normal) || normal.LengthSquared() < MIN_UNIT_LENGTH_SQ || !float.IsFinite(message.W))
                {
                    error = "the plane parameters are invalid";
                    return false;
                }

                shape = new ShapePlane(normal, message.W);
                return true;

            case ShapeType.ConvexHull:
                Float3Message[] points = message.Points ?? [];
                if (points.Length < 4)
                {
                    error = "the convex hull has fewer than four points";
                    return false;
                }

                var hullPoints = new Vector3[points.Length];
                for (var index = 0; index < points.Length; index++)
                {
                    var point = new Vector3(points[index].X, points[index].Y, points[index].Z);
                    if (!IsFinite(in point))
                    {
                        error = "the convex hull has a non-finite point";
                        return false;
                    }

                    hullPoints[index] = point;
                }

                shape = new ConvexHull(hullPoints);
                return true;

            default:
                error = $"the shape type {message.Type} is not a 3D collider";
                return false;
        }
    }

    private static bool IsPositiveFinite(float value)
    {
        return float.IsFinite(value) && value > 0f;
    }

    private static bool IsFinite(in Vector3 value)
    {
        return float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
    }

    private static bool IsFinite(in Quaternion value)
    {
        return float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z) && float.IsFinite(value.W);
    }
}

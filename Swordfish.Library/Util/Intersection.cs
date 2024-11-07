using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using Plane = Swordfish.Library.Types.Shapes.Plane;
// ReSharper disable UnusedMember.Global

namespace Swordfish.Library.Util;

public static class Intersection
{
    /// <summary>
    /// Perform a sphere-to-plane collision check
    /// </summary>
    /// <returns>true if the sphere is crossing the plane; otherwise false</returns>
    public static bool SphereToPlane(Vector3 center, float radius, Vector3 normal, Vector3 origin)
    {
        Vector3 relative = center - origin;
        float distance = Vector3.Dot(relative, normal);
        return Vector3.Dot(relative, normal) + distance + radius <= 0;
    }

    /// <summary>
    /// Perform a sphere-to-point collision check
    /// </summary>
    /// <returns>true if there is a collision; otherwise false</returns>
    public static bool SphereToPoint(Vector3 center, float radius, Vector3 point)
    {
        float distance = Vector3.Dot(center, point) - radius * radius;
        return distance <= 0;
    }

    /// <summary>
    /// Perform a sphere-to-line collision check
    /// </summary>
    /// <returns>true if there is a collision; otherwise false</returns>
    public static bool SphereToLine(Vector3 center, float radius, Vector3 start, Vector3 end)
    {
        //  First check if either points of the line are within the sphere
        if (SphereToPoint(center, radius, start) || SphereToPoint(center, radius, start))
        {
            return true;
        }

        Vector3 segment = end - start;

        //  Project sphere onto the line
        Vector3 projection = center - start;
        float projectedScale = Vector3.Dot(projection, segment) / Vector3.Dot(segment, segment);

        if (projectedScale < 0f || projectedScale > 1f)
        {
            return false;
        }

        //  Get the point nearest to the sphere
        Vector3 nearestPoint = start + projection * projectedScale;

        return SphereToPoint(center, radius, nearestPoint);
    }

    /// <summary>
    /// Perform a sphere-to-sphere collision check
    /// </summary>
    /// <returns>true if there is a collision; otherwise false</returns>
    public static bool SphereToSphere(Vector3 center1, float radius1, Vector3 center2, float radius2)
    {
        Vector3 relativeVector = center1 - center2;
        float range = radius1 + radius2;

        float distance = Vector3.Dot(relativeVector, relativeVector) - range * range;

        return distance <= 0;
    }

    /// <summary>
    /// Inaccurate and fast sweep using bounds to determine if two spheres can collide
    /// </summary>
    /// <returns>true if the spheres can collide; otherwise false</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool SweepSphereToSphere(Vector3 center1, float radius1, Vector3 center2, float radius2)
    {
        float range = radius1 + radius2;

        float overlapX = Math.Abs(center1.X - center2.X);
        if (overlapX > range)
        {
            return false;
        }

        float overlapY = Math.Abs(center1.Y - center2.Y);
        if (overlapY > range)
        {
            return false;
        }

        float overlapZ = Math.Abs(center1.Z - center2.Z);
        return !(overlapZ > range);
        //  Bounds must overlap on all 3 axis to be a possible collision
    }

    /// <summary>
    /// Perform a bounds-to-point collision check
    /// </summary>
    /// <returns>true if there is a collision; otherwise false</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool BoundingToPoint(Vector3 center, float size, Vector3 point)
    {
        if (Math.Abs(center.X - point.X) > size)
        {
            return false;
        }

        if (Math.Abs(center.Y - point.Y) > size)
        {
            return false;
        }

        return !(Math.Abs(center.Z - point.Z) > size);
        //  Bounds must overlap on all 3 axis to be a possible collision
    }

    /// <summary>
    /// Perform a plane-to-point collision check
    /// </summary>
    /// <param name="normal"></param>
    /// <param name="origin"></param>
    /// <param name="point"></param>
    /// <returns>true if the point is crossing the plane; otherwise false</returns>
    public static bool PlaneToPoint(Plane plane, Vector3 point)
    {
        Vector3 relative = point - plane.GetPosition();
        float distance = Vector3.Dot(relative, plane.Normal);
        return Vector3.Dot(relative, plane.Normal) + distance <= 0;
    }
}
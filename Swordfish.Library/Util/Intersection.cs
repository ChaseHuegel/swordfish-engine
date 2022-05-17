using System;
using System.Runtime.CompilerServices;

using OpenTK.Mathematics;

using Swordfish.Library.Types;

namespace Swordfish.Library.Util
{
    public static class Intersection
    {
        /// <summary>
        /// Perform a sphere-to-plane collision check
        /// </summary>
        /// <param name="center"></param>
        /// <param name="radius"></param>
        /// <param name="normal"></param>
        /// <param name="depth"></param>
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
        /// <param name="center"></param>
        /// <param name="radius"></param>
        /// <param name="point"></param>
        /// <returns>true if there is a collision; otherwise false</returns>
        public static bool SphereToPoint(Vector3 center, float radius, Vector3 point)
        {
            float distance = Vector3.Dot(center, point) - radius*radius;

            return distance <= 0;
        }

        /// <summary>
        /// Perform a sphere-to-line collision check
        /// </summary>
        /// <param name="center"></param>
        /// <param name="radius"></param>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <returns>true if there is a collision; otherwise false</returns>
        public static bool SphereToLine(Vector3 center, float radius, Vector3 start, Vector3 end)
        {
            //  First check if either points of the line are within the sphere
            if (SphereToPoint(center, radius, start) || SphereToPoint(center, radius, start))
                return true;

            Vector3 segment = end - start;

            //  Project sphere onto the line
            Vector3 projection = center - start;
            float projectedScale = Vector3.Dot(projection, segment) / Vector3.Dot(segment, segment);

            if (projectedScale < 0f || projectedScale > 1f)
                return false;

            //  Get the point nearest to the sphere
            Vector3 nearestPoint = start + (projection * projectedScale);

            return SphereToPoint(center, radius, nearestPoint);
        }

        /// <summary>
        /// Perform a sphere-to-sphere collision check
        /// </summary>
        /// <param name="center1"></param>
        /// <param name="radius1"></param>
        /// <param name="center2"></param>
        /// <param name="radius2"></param>
        /// <returns>true if there is a collision; otherwise false</returns>
        public static bool SphereToSphere(Vector3 center1, float radius1, Vector3 center2, float radius2)
        {
            Vector3 relativeVector = center1 - center2;
            float range = radius1 + radius2;

            float distance = Vector3.Dot(relativeVector, relativeVector) - range*range;

            return distance <= 0;
        }

        /// <summary>
        /// Inaccurate and fast sweep using boundings to determine if two spheres can collide
        /// </summary>
        /// <param name="center1"></param>
        /// <param name="radius1"></param>
        /// <param name="center2"></param>
        /// <param name="radius2"></param>
        /// <returns>true if the spheres can collide; otherwise false</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool SweepSphereToSphere(Vector3 center1, float radius1, Vector3 center2, float radius2)
        {
            float range = radius1 + radius2;

            float overlapX = Math.Abs(center1.X - center2.X);
            if (overlapX > range) return false;

            float overlapY = Math.Abs(center1.Y - center2.Y);
            if (overlapY > range) return false;

            float overlapZ = Math.Abs(center1.Z - center2.Z);
            if (overlapZ > range) return false;

            //  Boundings must overlap on all 3 axis to be a possible collision
            return true;
        }

        /// <summary>
        /// Perform a bounding-to-point collision check
        /// </summary>
        /// <param name="center"></param>
        /// <param name="size"></param>
        /// <param name="point"></param>
        /// <returns>true if there is a collision; otherwise false</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool BoundingToPoint(Vector3 center, float size, Vector3 point)
        {
            if (Math.Abs(center.X - point.X) > size) return false;
            if (Math.Abs(center.Y - point.Y) > size) return false;
            if (Math.Abs(center.Z - point.Z) > size) return false;

            //  Boundings must overlap on all 3 axis to be a possible collision
            return true;
        }

        /// <summary>
        /// Perform a plane-to-point collision check
        /// </summary>
        /// <param name="normal"></param>
        /// <param name="origin"></param>
        /// <param name="point"></param>
        /// <returns>true if the point is crossing the plane; otherwise false</returns>
        public static bool PlaneToPoint(Vector3 normal, Vector3 origin, Vector3 point)
        {
            Vector3 relative = point - origin;
            float distance = Vector3.Dot(relative, normal);

            return Vector3.Dot(relative, normal) + distance <= 0;
        }

        /// <summary>
        /// Perform a frustrum-to-point collision check
        /// </summary>
        /// <param name="planes"></param>
        /// <param name="point"></param>
        /// <returns>true if the frustrum contains the point; otherwise false</returns>
        public static bool FrustrumToPoint(Plane[] planes, Vector3 point)
        {
            foreach (Plane plane in planes)
                if (PlaneToPoint(plane.Normal, plane.Origin, point))
                    return false;

            return true;
        }
    }
}

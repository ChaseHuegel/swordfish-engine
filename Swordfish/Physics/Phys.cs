using System;
using OpenTK.Mathematics;

namespace Swordfish.Physics
{
    public static class Phys
    {
        /// <summary>
        /// Test for collision between two spheres using fast checks.
        /// </summary>
        /// <param name="p1"></param>
        /// <param name="p2"></param>
        /// <param name="radius1"></param>
        /// <param name="radius2"></param>
        /// <returns>instance of CollisionInfo with details about the collision; otherwise null if there is no collision</returns>
        public static CollisionInfo FastOverlapSphere(Vector3 p1, float radius1, Vector3 p2, float radius2)
        {
            float range = radius1 + radius2;
            float overlapX = p1.X - p2.X;
            float overlapY = p1.Y - p2.Y;
            float overlapZ = p1.Z - p2.Z;

            //  Sphere must overlap on all 3 axis to be a possible collision
            if (overlapX > range || overlapY > range || overlapZ > range)
                return null;

            //  Square the range since the distance will be squared
            range = (radius1 * radius1) + (radius2 * radius2);

            //  Get squared distance between the two points
            float distance = overlapX * overlapX +
                             overlapY * overlapY +
                             overlapZ * overlapZ;

            //  The radi must overlap or touch to be a collision
            if (distance > range)
                return null;

            //  Get the direction of the collision
            Vector3 normal = (p1 - p2); normal.NormalizeFast();

            //  Unsquare the range and distances to usable values
            range = (float)Math.Sqrt(range);
            distance = (float)Math.Sqrt(distance);

            //  Get the contact points
            Vector3 point = p2 + ( normal * range );
            Vector3 point2 = p1 - ( normal * range );

            return new CollisionInfo() { Contacts = new Vector3[] { point, point2 }, Normal = normal, Penetration = (range - distance) };
        }
    }
}

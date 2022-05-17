using System;
using OpenTK.Mathematics;

namespace Swordfish.Engine.Util
{
    public static class MathE
    {
        public static float Distance (Vector3 firstPosition, Vector3 secondPosition)
        {
            Vector3 heading;

            heading.X = firstPosition.X - secondPosition.X;
            heading.Y = firstPosition.Y - secondPosition.Y;
            heading.Z = firstPosition.Z - secondPosition.Z;

            float distanceSquared = heading.X * heading.X + heading.Y * heading.Y + heading.Z * heading.Z;
            return (float)Math.Sqrt(distanceSquared);
        }

        public static float DistanceUnsquared(Vector3 firstPosition, Vector3 secondPosition)
        {
            return (firstPosition.X - secondPosition.X) * (firstPosition.X - secondPosition.X) +
                    (firstPosition.Y - secondPosition.Y) * (firstPosition.Y - secondPosition.Y) +
                    (firstPosition.Z - secondPosition.Z) * (firstPosition.Z - secondPosition.Z);
        }

        public static Vector3[] CreateEllipse(float a, float b, float h, float k, float theta, int resolution)
        {
            Vector3[] positions = new Vector3[resolution+1];
            Quaternion q = Quaternion.FromAxisAngle(Vector3.UnitY, theta);
            Vector3 center = new Vector3(h,k,0.0f);

            for (int i = 0; i <= resolution; i++) {
                float angle = (float)i / (float)resolution * 2.0f * (float)Math.PI;
                positions[i] = new Vector3(a * (float)Math.Cos(angle), 0.0f, b * (float)Math.Sin(angle));
                positions[i] = q * positions[i] + center;
            }

            return positions;
        }
    }
}

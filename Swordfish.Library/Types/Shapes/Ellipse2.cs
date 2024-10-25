using System;
using System.Numerics;

namespace Swordfish.Library.Types.Shapes
{
    public struct Ellipse2
    {
        public float A;
        public float B;
        public float H;
        public float K;
        public float Theta;

        public Ellipse2(float a, float b, float h, float k, float theta)
        {
            A = a;
            B = b;
            H = h;
            K = k;
            Theta = theta;
        }

        public Vector3[] CreateVertices(int resolution)
        {
            Vector3[] points = new Vector3[resolution + 1];
            Quaternion q = Quaternion.CreateFromAxisAngle(Vector3.UnitY, Theta);
            Vector3 center = new Vector3(H, K, 0.0f);

            for (int i = 0; i <= resolution; i++)
            {
                float angle = i / (float)resolution * 2.0f * (float)Math.PI;
                points[i] = new Vector3(A * (float)Math.Cos(angle), 0.0f, B * (float)Math.Sin(angle));
                points[i] = Vector3.Transform(points[i], q) + center;
            }

            return points;
        }

        public static implicit operator Shape(Ellipse2 x) => new(x);
    }
}
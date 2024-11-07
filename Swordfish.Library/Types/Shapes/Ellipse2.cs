using System;
using System.Numerics;

namespace Swordfish.Library.Types.Shapes;

public struct Ellipse2(in float a, in float b, in float h, in float k, in float theta)
{
    public float A = a;
    public float B = b;
    public float H = h;
    public float K = k;
    public float Theta = theta;

    // ReSharper disable once UnusedMember.Global
    public Vector3[] CreateVertices(int resolution)
    {
        var points = new Vector3[resolution + 1];
        var q = Quaternion.CreateFromAxisAngle(Vector3.UnitY, Theta);
        var center = new Vector3(H, K, 0.0f);

        for (var i = 0; i <= resolution; i++)
        {
            float angle = i / (float)resolution * 2.0f * (float)Math.PI;
            points[i] = new Vector3(A * (float)Math.Cos(angle), 0.0f, B * (float)Math.Sin(angle));
            points[i] = Vector3.Transform(points[i], q) + center;
        }

        return points;
    }

    public static implicit operator Shape(Ellipse2 x) => new(x);
}
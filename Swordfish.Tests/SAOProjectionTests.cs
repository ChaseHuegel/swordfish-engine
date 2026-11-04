using System;
using System.Numerics;
using Swordfish.Graphics.SilkNET.OpenGL;
using Xunit;

namespace Swordfish.Tests;

public class SAOProjectionTests
{
    [Fact]
    public void MatchesTheInverseProjection()
    {
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(
            fieldOfView: MathF.PI / 2f,
            aspectRatio: 16f / 9f,
            nearPlaneDistance: 0.1f,
            farPlaneDistance: 1000f
        );
        Matrix4x4.Invert(projection, out Matrix4x4 inverse);

        SAOProjection constants = SAOProjection.FromProjection(projection);
        Vector2[] uvs =
        [
            new Vector2(0.5f, 0.5f),
            new Vector2(0.25f, 0.75f),
            new Vector2(0.9f, 0.1f),
        ];

        for (var i = 1; i < 32; i++)
        {
            float depth = i / 32f;

            foreach (Vector2 uv in uvs)
            {
                var ndc = new Vector4(uv.X * 2f - 1f, uv.Y * 2f - 1f, depth * 2f - 1f, 1f);
                Vector4 expected = Vector4.Transform(ndc, inverse);
                expected /= expected.W;

                float viewDistance = constants.DepthA / (constants.DepthB + ndc.Z);
                var viewNdc = new Vector2(ndc.X, ndc.Y);
                Vector2 actualXy = viewNdc * constants.ViewScale * viewDistance;
                var actual = new Vector3(actualXy, -viewDistance);

                AssertClose(expected.X, actual.X);
                AssertClose(expected.Y, actual.Y);
                AssertClose(expected.Z, actual.Z);
            }
        }
    }

    private static void AssertClose(float expected, float actual)
    {
        float tolerance = 0.0001f * MathF.Max(1f, MathF.Abs(expected));
        Assert.InRange(MathF.Abs(expected - actual), 0f, tolerance);
    }
}

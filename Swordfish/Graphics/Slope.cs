using System.Numerics;

namespace Swordfish.Graphics;

public class Slope : Mesh
{
    public Slope() : base(null!, null!, null!, null!, null!)
    {
        Triangles =
        [
            //  Top
            0,
            1,
            2,
            0,
            2,
            3,
            //  Bottom
            7,
            6,
            4,
            6,
            5,
            4,
            //  South
            11,
            10,
            8,
            10,
            9,
            8,
            //  East
            12,
            13,
            14,
            //  West
            17,
            16,
            15,
        ];

        Vertices =
        [
            //  Top
            new Vector3(-0.5f, 0.5f, 0.5f),
            new Vector3(0.5f, 0.5f, 0.5f),
            new Vector3(0.5f, -0.5f, -0.5f),
            new Vector3(-0.5f, -0.5f, -0.5f),
            //  Bottom
            new Vector3(-0.5f, -0.5f, 0.5f),
            new Vector3(0.5f, -0.5f, 0.5f),
            new Vector3(0.5f, -0.5f, -0.5f),
            new Vector3(-0.5f, -0.5f, -0.5f),
            //  South
            new Vector3(-0.5f, 0.5f, 0.5f),
            new Vector3(0.5f, 0.5f, 0.5f),
            new Vector3(0.5f, -0.5f, 0.5f),
            new Vector3(-0.5f, -0.5f, 0.5f),
            // East
            new Vector3(0.5f, 0.5f, 0.5f),
            new Vector3(0.5f, -0.5f, 0.5f),
            new Vector3(0.5f, -0.5f, -0.5f),
            // West
            new Vector3(-0.5f, 0.5f, 0.5f),
            new Vector3(-0.5f, -0.5f, 0.5f),
            new Vector3(-0.5f, -0.5f, -0.5f),
        ];

        Colors =
        [
            new Vector4(1f, 1f, 1f, 1f),
            new Vector4(1f, 1f, 1f, 1f),
            new Vector4(1f, 1f, 1f, 1f),
            new Vector4(1f, 1f, 1f, 1f),
            new Vector4(1f, 1f, 1f, 1f),
            new Vector4(1f, 1f, 1f, 1f),
            new Vector4(1f, 1f, 1f, 1f),
            new Vector4(1f, 1f, 1f, 1f),
            new Vector4(1f, 1f, 1f, 1f),
            new Vector4(1f, 1f, 1f, 1f),
            new Vector4(1f, 1f, 1f, 1f),
            new Vector4(1f, 1f, 1f, 1f),
            new Vector4(1f, 1f, 1f, 1f),
            new Vector4(1f, 1f, 1f, 1f),
            new Vector4(1f, 1f, 1f, 1f),
            new Vector4(1f, 1f, 1f, 1f),
            new Vector4(1f, 1f, 1f, 1f),
            new Vector4(1f, 1f, 1f, 1f),
        ];

        Normals =
        [
            new Vector3(0f, 1f, 0f),
            new Vector3(0f, 1f, 0f),
            new Vector3(0f, 1f, 0f),
            new Vector3(0f, 1f, 0f),

            new Vector3(0f, -1f, 0f),
            new Vector3(0f, -1f, 0f),
            new Vector3(0f, -1f, 0f),
            new Vector3(0f, -1f, 0f),

            new Vector3(0f, 0f, -1f),
            new Vector3(0f, 0f, -1f),
            new Vector3(0f, 0f, -1f),
            new Vector3(0f, 0f, -1f),

            new Vector3(1f, 0f, 0f),
            new Vector3(1f, 0f, 0f),
            new Vector3(1f, 0f, 0f),

            new Vector3(-1f, 0f, 0f),
            new Vector3(-1f, 0f, 0f),
            new Vector3(-1f, 0f, 0f),
        ];

        Uv =
        [
            new Vector3(0f, 1f, 0),
            new Vector3(1f, 1f, 0),
            new Vector3(1f, 0f, 0),
            new Vector3(0f, 0f, 0),
            new Vector3(0f, 1f, 0),
            new Vector3(1f, 1f, 0),
            new Vector3(1f, 0f, 0),
            new Vector3(0f, 0f, 0),
            new Vector3(0f, 1f, 0),
            new Vector3(1f, 1f, 0),
            new Vector3(1f, 0f, 0),
            new Vector3(0f, 0f, 0),
            new Vector3(1f, 1f, 0),
            new Vector3(1f, 0f, 0),
            new Vector3(0f, 0f, 0),
            new Vector3(1f, 1f, 0),
            new Vector3(1f, 0f, 0),
            new Vector3(0f, 0f, 0),
        ];
    }
}

using System.Numerics;

namespace Swordfish.Graphics;

public class Slope : Mesh
{
    public Slope() : base(null!, null!, null!, null!, null!)
    {
        Triangles = new uint[]
        {
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
        };

        Vertices = new Vector3[]
        {
            //  Top
            new Vector3(0, 1, 1),
            new Vector3(1, 1, 1),
            new Vector3(1, 0, 0),
            new Vector3(0, 0, 0),
            //  Bottom
            new Vector3(0, 0, 1),
            new Vector3(1, 0, 1),
            new Vector3(1, 0, 0),
            new Vector3(0, 0, 0),
            //  South
            new Vector3(0, 1, 1),
            new Vector3(1, 1, 1),
            new Vector3(1, 0, 1),
            new Vector3(0, 0, 1),
            // East
            new Vector3(1, 1, 1),
            new Vector3(1, 0, 1),
            new Vector3(1, 0, 0),
            // West
            new Vector3(0, 1, 1),
            new Vector3(0, 0, 1),
            new Vector3(0, 0, 0),
        };

        Colors = new Vector4[]
        {
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
        };

        Normals = new Vector3[]
        {
            new Vector3(0f, 1f, 0f),
            new Vector3(0f, 1f, 0f),
            new Vector3(0f, 1f, 0f),
            new Vector3(0f, 1f, 0f),
            new Vector3(0f, -1f, 0f),
            new Vector3(0f, -1f, 0f),
            new Vector3(0f, -1f, 0f),
            new Vector3(0f, -1f, 0f),
            new Vector3(0f, 0f, 1f),
            new Vector3(0f, 0f, 1f),
            new Vector3(0f, 0f, 1f),
            new Vector3(0f, 0f, 1f),
            new Vector3(1f, 0f, 0f),
            new Vector3(1f, 0f, 0f),
            new Vector3(1f, 0f, 0f),
            new Vector3(-1f, 0f, 0f),
            new Vector3(-1f, 0f, 0f),
            new Vector3(-1f, 0f, 0f),
        };

        UV = new Vector3[]
        {
            new Vector3(0f, 1f, 19),
            new Vector3(1f, 1f, 19),
            new Vector3(1f, 0f, 19),
            new Vector3(0f, 0f, 19),
            new Vector3(0f, 1f, 1),
            new Vector3(1f, 1f, 1),
            new Vector3(1f, 0f, 1),
            new Vector3(0f, 0f, 1),
            new Vector3(0f, 1f, 3),
            new Vector3(1f, 1f, 3),
            new Vector3(1f, 0f, 3),
            new Vector3(0f, 0f, 3),
            new Vector3(1f, 1f, 4),
            new Vector3(1f, 0f, 4),
            new Vector3(0f, 0f, 4),
            new Vector3(1f, 1f, 5),
            new Vector3(1f, 0f, 5),
            new Vector3(0f, 0f, 5),
        };
    }
}

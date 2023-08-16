using System.Numerics;

namespace Swordfish.Graphics;

public class Cube : Mesh
{
    public Cube() : base(null!, null!, null!, null!, null!)
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
            //  North
            8,
            9,
            10,
            8,
            10,
            11,
            //  South
            15,
            14,
            12,
            14,
            13,
            12,
            //  East
            16,
            17,
            18,
            16,
            18,
            19,
            //  West
            23,
            22,
            20,
            22,
            21,
            20,
        };

        Vertices = new Vector3[]
        {
            new Vector3(-0.5f, 0.5f, 0.5f),
            new Vector3(0.5f, 0.5f, 0.5f),
            new Vector3(0.5f, 0.5f, -0.5f),
            new Vector3(-0.5f, 0.5f, -0.5f),
            new Vector3(-0.5f, -0.5f, 0.5f),
            new Vector3(0.5f, -0.5f, 0.5f),
            new Vector3(0.5f, -0.5f, -0.5f),
            new Vector3(-0.5f, -0.5f, -0.5f),
            new Vector3(-0.5f, 0.5f, -0.5f),
            new Vector3(0.5f, 0.5f, -0.5f),
            new Vector3(0.5f, -0.5f, -0.5f),
            new Vector3(-0.5f, -0.5f, -0.5f),
            new Vector3(-0.5f, 0.5f, 0.5f),
            new Vector3(0.5f, 0.5f, 0.5f),
            new Vector3(0.5f, -0.5f, 0.5f),
            new Vector3(-0.5f, -0.5f, 0.5f),
            new Vector3(0.5f, 0.5f, -0.5f),
            new Vector3(0.5f, 0.5f, 0.5f),
            new Vector3(0.5f, -0.5f, 0.5f),
            new Vector3(0.5f, -0.5f, -0.5f),
            new Vector3(-0.5f, 0.5f, -0.5f),
            new Vector3(-0.5f, 0.5f, 0.5f),
            new Vector3(-0.5f, -0.5f, 0.5f),
            new Vector3(-0.5f, -0.5f, -0.5f),
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
            new Vector3(0f, 0f, -1f),
            new Vector3(0f, 0f, -1f),
            new Vector3(0f, 0f, -1f),
            new Vector3(0f, 0f, -1f),
            new Vector3(0f, 0f, 1f),
            new Vector3(0f, 0f, 1f),
            new Vector3(0f, 0f, 1f),
            new Vector3(0f, 0f, 1f),
            new Vector3(1f, 0f, 0f),
            new Vector3(1f, 0f, 0f),
            new Vector3(1f, 0f, 0f),
            new Vector3(1f, 0f, 0f),
            new Vector3(-1f, 0f, 0f),
            new Vector3(-1f, 0f, 0f),
            new Vector3(-1f, 0f, 0f),
            new Vector3(-1f, 0f, 0f),
        };

        UV = new Vector3[]
        {
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
            new Vector3(0f, 0f, 0),
            new Vector3(1f, 0f, 0),
        };
    }
}

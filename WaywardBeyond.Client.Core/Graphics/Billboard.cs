using System.Numerics;
using Swordfish.Graphics;

namespace WaywardBeyond.Client.Core.Graphics;

public sealed class Billboard : Mesh
{
    public Billboard() : base(null!, null!, null!, null!, null!)
    {
        Triangles =
        [
            0,
            1,
            2,
            0,
            2,
            3,
        ];

        Vertices =
        [
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3( 0.5f, -0.5f, 0f),
            new Vector3( 0.5f,  0.5f, 0f),
            new Vector3(-0.5f,  0.5f, 0f)
        ];

        Colors =
        [
            new Vector4(1f, 1f, 1f, 1f),
            new Vector4(1f, 1f, 1f, 1f),
            new Vector4(1f, 1f, 1f, 1f),
            new Vector4(1f, 1f, 1f, 1f),
        ];

        Normals =
        [
            new Vector3(0f, 0f, 1f),
            new Vector3(0f, 0f, 1f),
            new Vector3(0f, 0f, 1f),
            new Vector3(0f, 0f, 1f),
        ];

        Uv =
        [
            new Vector3(0f, 0f, 0f),
            new Vector3(1f, 0f, 0f),
            new Vector3(1f, 1f, 0f),
            new Vector3(0f, 1f, 0f),
        ];
    }
}
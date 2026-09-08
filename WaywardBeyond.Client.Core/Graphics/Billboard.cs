using System.Numerics;
using Swordfish.Graphics;

namespace WaywardBeyond.Client.Core.Graphics;

/// <summary>
/// A single camera-facing quad plane (two triangles spanning the XY plane, normal +Z) used for billboards.
/// Built programmatically so remote-player visuals need no asset file. Mesh is shared across all billboards.
/// </summary>
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

        //  Quad spans [-0.5, 0.5] x [-0.5, 0.5] in the XY plane, facing +Z. Scale is applied at render time.
        Vertices =
        [
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f),
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
            new Vector3(0f, 1f, 0f),
            new Vector3(1f, 1f, 0f),
            new Vector3(1f, 0f, 0f),
            new Vector3(0f, 0f, 0f),
        ];
    }
}
using System.Numerics;
using Swordfish.Library.Types;

namespace Swordfish.Graphics;

public class Mesh : Handle
{
    public uint[] Triangles = [];
    public Vector3[] Vertices = [];
    public Vector4[] Colors = [];
    public Vector3[] Uv = [];
    public Vector3[] Normals = [];

    public Mesh(uint[] triangles, Vector3[] vertices, Vector4[] colors, Vector3[] uv, Vector3[] normals)
    {
        Triangles = triangles;
        Vertices = vertices;
        Colors = colors;
        Uv = uv;
        Normals = normals;
    }

    protected override void OnDisposed()
    {
    }
}

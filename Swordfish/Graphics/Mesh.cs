using System.Numerics;

namespace Swordfish.Graphics;

public class Mesh : Handle
{
    public uint[] Triangles = Array.Empty<uint>();
    public Vector3[] Vertices = Array.Empty<Vector3>();
    public Vector4[] Colors = Array.Empty<Vector4>();
    public Vector3[] UV = Array.Empty<Vector3>();
    public Vector3[] Normals = Array.Empty<Vector3>();

    public Mesh(uint[] triangles, Vector3[] vertices, Vector4[] colors, Vector3[] uv, Vector3[] normals)
    {
        Triangles = triangles;
        Vertices = vertices;
        Colors = colors;
        UV = uv;
        Normals = normals;
    }

    protected override void OnDisposed()
    {
    }
}

using System.Numerics;

namespace Swordfish.Graphics;

public class Mesh : Handle
{
    public uint[] Triangles = Array.Empty<uint>();
    public Vector3[] Vertices = Array.Empty<Vector3>();
    public Vector4[] Colors = Array.Empty<Vector4>();
    public Vector3[] UV = Array.Empty<Vector3>();
    public Vector3[] Normals = Array.Empty<Vector3>();

    protected override void OnDisposed()
    {
    }
}

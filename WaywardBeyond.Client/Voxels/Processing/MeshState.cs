using System.Collections.Generic;
using System.Numerics;

namespace WaywardBeyond.Client.Voxels.Processing;

internal sealed class MeshState
{
    public readonly MeshData Opaque = new();
    public readonly MeshData Masked = new();
    public readonly MeshData Transparent = new();

    public void Clear()
    {
        Opaque.Clear();
        Masked.Clear();
        Transparent.Clear();
    }
    
    public sealed class MeshData
    {
        public readonly List<uint> Triangles = [];
        public readonly List<Vector3> Vertices = [];
        public readonly List<Vector4> Colors = [];
        public readonly List<Vector3> UV = [];
        public readonly List<Vector3> Normals = [];

        public void Clear()
        {
            Triangles.Clear();
            Vertices.Clear();
            Colors.Clear();
            UV.Clear();
            Normals.Clear();
        }
    }
}
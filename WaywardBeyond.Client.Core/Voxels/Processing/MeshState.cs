using System.Collections.Generic;
using System.Numerics;

namespace WaywardBeyond.Client.Core.Voxels.Processing;

internal sealed class MeshState
{
    public readonly MeshData Opaque = new();
    public readonly MeshData Transparent = new();
    
    public sealed class MeshData
    {
        public readonly List<uint> Triangles = [];
        public readonly List<Vector3> Vertices = [];
        public readonly List<Vector4> Colors = [];
        public readonly List<Vector3> UV = [];
        public readonly List<Vector3> Normals = [];
    }
}
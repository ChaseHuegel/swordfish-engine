using System.Collections.Generic;
using System.Numerics;

namespace WaywardBeyond.Meshing;

/// <summary>Holds reusable per-alpha-bucket mesh builders accumulated during voxel meshing.</summary>
public sealed class MeshState
{
    /// <summary>Geometry bucket for opaque shadow-casting bricks.</summary>
    public readonly MeshData Opaque = new();

    /// <summary>Geometry bucket for opaque bricks that do not cast shadows.</summary>
    public readonly MeshData OpaqueNoShadow = new();

    /// <summary>Geometry bucket for alpha-masked bricks.</summary>
    public readonly MeshData Masked = new();

    /// <summary>Geometry bucket for alpha-blended bricks.</summary>
    public readonly MeshData Transparent = new();

    /// <summary>Clears all four geometry buckets while retaining their backing list capacities.</summary>
    public void Clear()
    {
        Opaque.Clear();
        OpaqueNoShadow.Clear();
        Masked.Clear();
        Transparent.Clear();
    }

    /// <summary>Holds accumulated geometry lists and reusable packed staging buffers for one render bucket.</summary>
    public sealed class MeshData
    {
        /// <summary>Accumulated triangle vertex indices.</summary>
        public readonly List<uint> Triangles = [];

        /// <summary>Accumulated vertex positions.</summary>
        public readonly List<Vector3> Vertices = [];

        /// <summary>Accumulated vertex colors (linear tint in RGB, baked light in A).</summary>
        public readonly List<Vector4> Colors = [];

        /// <summary>Accumulated vertex UVs with texture-array layer index in Z.</summary>
        public readonly List<Vector3> UV = [];

        /// <summary>Accumulated vertex normals.</summary>
        public readonly List<Vector3> Normals = [];

        /// <summary>Reusable interleaved vertex staging buffer for callers that pack vertices.</summary>
        public float[] VertexBuffer = [];

        /// <summary>Reusable index staging buffer for callers that pack indices.</summary>
        public uint[] IndexBuffer = [];

        /// <summary>Clears all accumulated vertex and triangle lists.</summary>
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

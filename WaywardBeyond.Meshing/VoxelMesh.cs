using System.Numerics;

namespace WaywardBeyond.Meshing;

/// <summary>Holds CPU-side triangle geometry, vertex colors, texture-array UVs, and normals for a meshed voxel surface.</summary>
public readonly struct VoxelMesh(
    uint[] triangles,
    Vector3[] vertices,
    Vector4[] colors,
    Vector3[] uv,
    Vector3[] normals
) {
    /// <summary>Triangle vertex indices.</summary>
    public readonly uint[] Triangles = triangles;

    /// <summary>Local-space vertex positions.</summary>
    public readonly Vector3[] Vertices = vertices;

    /// <summary>Per-vertex linear albedo tint in RGB and baked voxel light in A.</summary>
    public readonly Vector4[] Colors = colors;

    /// <summary>Per-vertex texture coordinates in XY and texture-array layer index in Z.</summary>
    public readonly Vector3[] Uv = uv;

    /// <summary>Per-vertex unit surface normals.</summary>
    public readonly Vector3[] Normals = normals;

    /// <summary>True when this mesh contains no vertices.</summary>
    public bool IsEmpty => Vertices == null || Vertices.Length == 0;
}

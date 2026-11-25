using Swordfish.Graphics;
using WaywardBeyond.Meshing;

namespace WaywardBeyond.Client.Voxels.Building;

/// <summary>Converts shared CPU <see cref="VoxelMesh"/> geometry into engine <see cref="Mesh"/> instances.</summary>
internal static class VoxelMeshExtensions
{
    /// <summary>Creates a new engine <see cref="Mesh"/> backed by the arrays in <paramref name="mesh"/>.</summary>
    public static Mesh ToEngineMesh(this in VoxelMesh mesh)
    {
        return new Mesh(mesh.Triangles, mesh.Vertices, mesh.Colors, mesh.Uv, mesh.Normals);
    }
}

using System.Collections.Generic;

using OpenTK.Mathematics;

using Swordfish.Engine.Rendering;
using Swordfish.Engine.Rendering.Shapes;

namespace Swordfish.Engine.Voxels
{
    public class VoxelObject
    {
        public Dictionary<Vector3i, Voxel> voxels = new Dictionary<Vector3i, Voxel>();

        public Mesh Mesh = new Mesh();

        public VoxelObject()
        {
            voxels.Add(new Vector3i(0, 0, 0), Voxel.SOLID);
            voxels.Add(new Vector3i(1, 0, 0), Voxel.SOLID);
            voxels.Add(new Vector3i(2, 0, 0), Voxel.SOLID);
            voxels.Add(new Vector3i(3, 0, 0), Voxel.SOLID);
            voxels.Add(new Vector3i(4, 0, 0), Voxel.SOLID);
            voxels.Add(new Vector3i(0, 1, 0), Voxel.SOLID);
            voxels.Add(new Vector3i(0, 2, 0), Voxel.SOLID);
            voxels.Add(new Vector3i(0, 3, 0), Voxel.SOLID);
            voxels.Add(new Vector3i(0, 4, 0), Voxel.SOLID);
            voxels.Add(new Vector3i(0, 0, 1), Voxel.SOLID);
            voxels.Add(new Vector3i(0, 0, 2), Voxel.SOLID);
            voxels.Add(new Vector3i(0, 0, 3), Voxel.SOLID);
            voxels.Add(new Vector3i(0, 0, 4), Voxel.SOLID);
        }

        public void BuildMesh()
        {
            Mesh.Name = "VoxelObject";

            List<uint> triangles = new List<uint>();
            List<Vector3> vertices = new List<Vector3>();
            List<Vector4> colors = new List<Vector4>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector3> uv = new List<Vector3>();

            Cube cube = new Cube();

            foreach (KeyValuePair<Vector3i, Voxel> pair in voxels)
            {
                if (pair.Value == Voxel.SOLID)
                {
                    colors.AddRange(cube.colors);
                    normals.AddRange(cube.normals);
                    uv.AddRange(cube.uv);

                    foreach (uint tri in cube.triangles)
                        triangles.Add(tri + (uint)vertices.Count);

                    foreach (Vector3 vertex in cube.vertices)
                        vertices.Add(vertex + pair.Key);
                }
            }

            Mesh.triangles = triangles.ToArray();
            Mesh.vertices = vertices.ToArray();
            Mesh.normals = normals.ToArray();
            Mesh.uv = uv.ToArray();
            Mesh.colors = colors.ToArray();
        }
    }
}

using OpenTK.Mathematics;

namespace Swordfish.Rendering.Shapes
{
    public class Quad : Mesh
    {
        public Quad()
        {
            Name = "Plane";

            Origin = new Vector3(-0.5f, -0.5f, 0f);

            triangles = new uint[] {
                0, 1, 2, 0, 2, 3,
            };

            vertices = new Vector3[] {
                new Vector3(0, 1, 0),
                new Vector3(1, 1, 0),
                new Vector3(1, 0, 0),
                new Vector3(0, 0, 0),
            };

            colors = new Vector4[] {
                new Vector4(1f, 1f, 1f, 1f),
                new Vector4(1f, 1f, 1f, 1f),
                new Vector4(1f, 1f, 1f, 1f),
                new Vector4(1f, 1f, 1f, 1f),
            };

            normals = new Vector3[] {
                new Vector3(0f, 0f, 1f),
                new Vector3(0f, 0f, 1f),
                new Vector3(0f, 0f, 1f),
                new Vector3(0f, 0f, 1f),
            };

            uv = new Vector3[] {
                new Vector3(0f, 1f, 2),
                new Vector3(1f, 1f, 2),
                new Vector3(1f, 0f, 2),
                new Vector3(0f, 0f, 2),
            };
        }
    }
}
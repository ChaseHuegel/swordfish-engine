using OpenTK.Mathematics;

namespace Swordfish.Engine.Rendering.Shapes
{
    public class Cube : Mesh
    {
        public Cube()
        {
            Name = "Cube";

            Origin = new Vector3(-0.5f, -0.5f, -0.5f);

            triangles = new uint[] {
                //  Top
                0, 1, 2, 0, 2, 3,

                //  Bottom
                7, 6, 4, 6, 5, 4,

                //  North
                8, 9, 10, 8, 10, 11,

                //  South
                15, 14, 12, 14, 13, 12,

                //  East
                16, 17, 18, 16, 18, 19,

                //  West
                23, 22, 20, 22, 21, 20,
            };

            vertices = new Vector3[] {
                new Vector3(0, 1, 1),
                new Vector3(1, 1, 1),
                new Vector3(1, 1, 0),
                new Vector3(0, 1, 0),

                new Vector3(0, 0, 1),
                new Vector3(1, 0, 1),
                new Vector3(1, 0, 0),
                new Vector3(0, 0, 0),

                new Vector3(0, 1, 0),
                new Vector3(1, 1, 0),
                new Vector3(1, 0, 0),
                new Vector3(0, 0, 0),

                new Vector3(0, 1, 1),
                new Vector3(1, 1, 1),
                new Vector3(1, 0, 1),
                new Vector3(0, 0, 1),

                new Vector3(1, 1, 0),
                new Vector3(1, 1, 1),
                new Vector3(1, 0, 1),
                new Vector3(1, 0, 0),

                new Vector3(0, 1, 0),
                new Vector3(0, 1, 1),
                new Vector3(0, 0, 1),
                new Vector3(0, 0, 0),
            };

            colors = new Vector4[] {
                new Vector4(1f, 1f, 1f, 1f),
                new Vector4(1f, 1f, 1f, 1f),
                new Vector4(1f, 1f, 1f, 1f),
                new Vector4(1f, 1f, 1f, 1f),

                new Vector4(1f, 1f, 1f, 1f),
                new Vector4(1f, 1f, 1f, 1f),
                new Vector4(1f, 1f, 1f, 1f),
                new Vector4(1f, 1f, 1f, 1f),

                new Vector4(1f, 1f, 1f, 1f),
                new Vector4(1f, 1f, 1f, 1f),
                new Vector4(1f, 1f, 1f, 1f),
                new Vector4(1f, 1f, 1f, 1f),

                new Vector4(1f, 1f, 1f, 1f),
                new Vector4(1f, 1f, 1f, 1f),
                new Vector4(1f, 1f, 1f, 1f),
                new Vector4(1f, 1f, 1f, 1f),

                new Vector4(1f, 1f, 1f, 1f),
                new Vector4(1f, 1f, 1f, 1f),
                new Vector4(1f, 1f, 1f, 1f),
                new Vector4(1f, 1f, 1f, 1f),

                new Vector4(1f, 1f, 1f, 1f),
                new Vector4(1f, 1f, 1f, 1f),
                new Vector4(1f, 1f, 1f, 1f),
                new Vector4(1f, 1f, 1f, 1f),
            };

            normals = new Vector3[] {
                new Vector3(0f, 1f, 0f),
                new Vector3(0f, 1f, 0f),
                new Vector3(0f, 1f, 0f),
                new Vector3(0f, 1f, 0f),

                new Vector3(0f, -1f, 0f),
                new Vector3(0f, -1f, 0f),
                new Vector3(0f, -1f, 0f),
                new Vector3(0f, -1f, 0f),

                new Vector3(0f, 0f, -1f),
                new Vector3(0f, 0f, -1f),
                new Vector3(0f, 0f, -1f),
                new Vector3(0f, 0f, -1f),

                new Vector3(0f, 0f, 1f),
                new Vector3(0f, 0f, 1f),
                new Vector3(0f, 0f, 1f),
                new Vector3(0f, 0f, 1f),

                new Vector3(1f, 0f, 0f),
                new Vector3(1f, 0f, 0f),
                new Vector3(1f, 0f, 0f),
                new Vector3(1f, 0f, 0f),

                new Vector3(-1f, 0f, 0f),
                new Vector3(-1f, 0f, 0f),
                new Vector3(-1f, 0f, 0f),
                new Vector3(-1f, 0f, 0f),
            };

            uv = new Vector3[] {
                new Vector3(0f, 1f, 19),
                new Vector3(1f, 1f, 19),
                new Vector3(1f, 0f, 19),
                new Vector3(0f, 0f, 19),

                new Vector3(0f, 1f, 1),
                new Vector3(1f, 1f, 1),
                new Vector3(1f, 0f, 1),
                new Vector3(0f, 0f, 1),

                new Vector3(0f, 1f, 2),
                new Vector3(1f, 1f, 2),
                new Vector3(1f, 0f, 2),
                new Vector3(0f, 0f, 2),

                new Vector3(0f, 1f, 3),
                new Vector3(1f, 1f, 3),
                new Vector3(1f, 0f, 3),
                new Vector3(0f, 0f, 3),

                new Vector3(0f, 1f, 4),
                new Vector3(1f, 1f, 4),
                new Vector3(1f, 0f, 4),
                new Vector3(0f, 0f, 4),

                new Vector3(0f, 1f, 5),
                new Vector3(1f, 1f, 5),
                new Vector3(1f, 0f, 5),
                new Vector3(0f, 0f, 5),
            };
        }
    }
}
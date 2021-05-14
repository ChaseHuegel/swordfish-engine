using System;
using System.Collections;
using System.Collections.Generic;
using OpenTK.Mathematics;
using Swordfish;

namespace Swordfish.Rendering.Shapes
{
    public class Cube : Mesh
    {
        public Cube()
        {
            origin = new Vector3(-0.5f, -0.5f, -0.5f);

            triangles = new uint[] {
                //  Top
                0, 1, 2, 0, 2, 3,

                //  Bottom
                4, 5, 6, 4, 6, 7,

                //  North
                8, 9, 10, 8, 10, 11,

                //  South
                12, 13, 14, 12, 14, 15,

                //  East
                16, 17, 18, 16, 18, 19,

                //  West
                20, 21, 22, 20, 22, 23,
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
                new Vector3(0f, 0f, 0),
                new Vector3(1f, 0f, 0),
                new Vector3(1f, 1f, 0),
                new Vector3(0f, 1f, 0),

                new Vector3(0f, 0f, 1),
                new Vector3(1f, 0f, 1),
                new Vector3(1f, 1f, 1),
                new Vector3(0f, 1f, 1),

                new Vector3(0f, 0f, 2),
                new Vector3(1f, 0f, 2),
                new Vector3(1f, 1f, 2),
                new Vector3(0f, 1f, 2),

                new Vector3(0f, 0f, 3),
                new Vector3(1f, 0f, 3),
                new Vector3(1f, 1f, 3),
                new Vector3(0f, 1f, 3),

                new Vector3(0f, 0f, 4),
                new Vector3(1f, 0f, 4),
                new Vector3(1f, 1f, 4),
                new Vector3(0f, 1f, 4),

                new Vector3(0f, 0f, 5),
                new Vector3(1f, 0f, 5),
                new Vector3(1f, 1f, 5),
                new Vector3(0f, 1f, 5),
            };
        }
    }
}
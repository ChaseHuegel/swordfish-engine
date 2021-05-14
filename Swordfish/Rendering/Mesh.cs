using System;
using System.Collections;
using System.Collections.Generic;
using OpenTK.Mathematics;
using Swordfish;

namespace Swordfish.Rendering
{
    public class MeshData
    {
        public uint[] triangles;
        public float[] vertices;

        public MeshData(uint[] triangles, float[] vertices)
        {
            this.triangles = triangles;
            this.vertices = vertices;
        }
    }

    public class Mesh
    {
        public Vector3 origin = Vector3.Zero;

        public uint[] triangles;
        public Vector3[] vertices;
        public Vector4[] colors;
        public Vector3[] normals;
        public Vector3[] uv;

        public MeshData GetRawData()
        {
            float[] raw = new float[ vertices.Length * 13 ];

            int row;
            for (int i = 0; i < vertices.Length; i++)
            {
                row = i * 13;
                raw[row] = vertices[i].X + origin.X;
                raw[row+1] = vertices[i].Y + origin.Y;
                raw[row+2] = vertices[i].Z + origin.Z;

                raw[row+3] = colors[i].X;
                raw[row+4] = colors[i].Y;
                raw[row+5] = colors[i].Z;
                raw[row+6] = colors[i].W;

                raw[row+7] = normals[i].X;
                raw[row+8] = normals[i].Y;
                raw[row+9] = normals[i].Z;

                raw[row+10] = uv[i].X;
                raw[row+11] = uv[i].Y;
                raw[row+12] = uv[i].Z;
            }

            return new MeshData(triangles, raw);
        }
    }
}
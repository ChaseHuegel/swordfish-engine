using System.IO;
using System.Collections.Generic;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using Swordfish.Diagnostics;
using System.Linq;
using Swordfish.Types;

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
        public string Name = "";
        public bool DoubleSided = true;
        public Vector3 Origin = Vector3.Zero;

        public Shader Shader;
        public Texture Texture;

        public uint[] triangles;
        public Vector3[] vertices;
        public Vector4[] colors;
        public Vector3[] normals;
        public Vector3[] uv;

        internal int VAO, VBO, EBO;

        /// <summary>
        /// Translates the mesh into data for using directly with openGL
        /// </summary>
        /// <returns></returns>
        public MeshData GetRawData()
        {
            float[] raw = new float[ vertices.Length * 13 ];

            int row;
            for (int i = 0; i < vertices.Length; i++)
            {
                row = i * 13;
                raw[row] = vertices[i].X + Origin.X;
                raw[row+1] = vertices[i].Y + Origin.Y;
                raw[row+2] = vertices[i].Z + Origin.Z;

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

        /// <summary>
        /// Bind this mesh to openGL data buffers
        /// </summary>
        internal void Bind()
        {
            MeshData data = GetRawData();

            //  Setup vertex buffer
            VBO = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, VBO);
            GL.BufferData(BufferTarget.ArrayBuffer, data.vertices.Length * sizeof(float), data.vertices, BufferUsageHint.StaticDraw);

            //  Setup VAO and tell openGL how to interpret vertex data
            VAO = GL.GenVertexArray();
            GL.BindVertexArray(VAO);

            int attrib = Shader.GetAttribLocation("in_position");
            GL.VertexAttribPointer(attrib, 3, VertexAttribPointerType.Float, false, 13 * sizeof(float), 0);
            GL.EnableVertexAttribArray(attrib);

            attrib = Shader.GetAttribLocation("in_color");
            GL.VertexAttribPointer(attrib, 4, VertexAttribPointerType.Float, false, 13 * sizeof(float), 3 * sizeof(float));
            GL.EnableVertexAttribArray(attrib);

            attrib = Shader.GetAttribLocation("in_uv");
            GL.VertexAttribPointer(attrib, 3, VertexAttribPointerType.Float, false, 13 * sizeof(float), 10 * sizeof(float));
            GL.EnableVertexAttribArray(attrib);

            //  Setup element buffer
            EBO = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, EBO);
            GL.BufferData(BufferTarget.ElementArrayBuffer, data.triangles.Length * sizeof(uint), data.triangles, BufferUsageHint.StaticDraw);
        }

        /// <summary>
        /// Make a draw call to render this mesh using openGL
        /// <para/> This must be used within a rendering context, this is not immediate call
        /// </summary>
        internal void Render()
        {
            if (DoubleSided)
                GL.Disable(EnableCap.CullFace);
            else
                GL.Enable(EnableCap.CullFace);

            Shader.Use();
            Texture.Use(TextureUnit.Texture0);

            GL.BindVertexArray(VAO);
            GL.DrawElements(PrimitiveType.Triangles, triangles.Length, DrawElementsType.UnsignedInt, 0);
        }
    }
}
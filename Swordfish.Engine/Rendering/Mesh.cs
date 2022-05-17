using System.Collections.Generic;

using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

using Swordfish.Library.Util;

namespace Swordfish.Engine.Rendering
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
        internal int VAO, VBO, EBO;

        private bool boundToGL = false;

        public uint[] triangles;
        public Vector3[] vertices;
        public Vector4[] colors;
        public Vector3[] normals;
        public Vector3[] uv;

        public Vector2 uvOffset;

        public string Name = "";
        public Vector3 Origin = Vector3.Zero;
        public Vector3 Scale = Vector3.One;

        public List<Material> Materials = new List<Material>();
        public Material Material
        {
            get => Materials.Count > 0 ? Materials[0] : null;
            set
            {
                if (Materials.Count > 0)
                    Materials[0] = value;
                else
                    Materials.Add(value);
            }
        }

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
                raw[row] = (vertices[i].X + Origin.X) * Scale.X;
                raw[row+1] = (vertices[i].Y + Origin.Y) * Scale.Y;
                raw[row+2] = (vertices[i].Z + Origin.Z) * Scale.Z;

                raw[row+3] = colors[i].X;
                raw[row+4] = colors[i].Y;
                raw[row+5] = colors[i].Z;
                raw[row+6] = colors[i].W;

                raw[row+7] = normals[i].X;
                raw[row+8] = normals[i].Y;
                raw[row+9] = normals[i].Z;

                raw[row+10] = uv[i].X + uvOffset.X;
                raw[row+11] = uv[i].Y + uvOffset.Y;
                raw[row+12] = uv[i].Z;
            }

            return new MeshData(triangles, raw);
        }

        /// <summary>
        /// Bind this mesh to openGL data buffers
        /// </summary>
        internal Mesh Bind()
        {
            //  Don't rebind, the user or renderer has already binded this Mesh
            if (boundToGL) return this;

            MeshData data = GetRawData();

            //  Setup vertex buffer
            VBO = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, VBO);
            GL.BufferData(BufferTarget.ArrayBuffer, data.vertices.Length * sizeof(float), data.vertices, BufferUsageHint.StaticDraw);

            //  Setup VAO and tell openGL how to interpret vertex data
            VAO = GL.GenVertexArray();
            GL.BindVertexArray(VAO);

            int attrib;
            foreach (Material m in Materials)
            {
                attrib = m.Shader.GetAttribLocation("in_position");
                GL.VertexAttribPointer(attrib, 3, VertexAttribPointerType.Float, false, 13 * sizeof(float), 0);
                GL.EnableVertexAttribArray(attrib);

                attrib = m.Shader.GetAttribLocation("in_color");
                GL.VertexAttribPointer(attrib, 4, VertexAttribPointerType.Float, false, 13 * sizeof(float), 3 * sizeof(float));
                GL.EnableVertexAttribArray(attrib);

                attrib = m.Shader.GetAttribLocation("in_normal");
                GL.VertexAttribPointer(attrib, 3, VertexAttribPointerType.Float, false, 13 * sizeof(float), 7 * sizeof(float));
                GL.EnableVertexAttribArray(attrib);

                attrib = m.Shader.GetAttribLocation("in_uv");
                GL.VertexAttribPointer(attrib, 3, VertexAttribPointerType.Float, false, 13 * sizeof(float), 10 * sizeof(float));
                GL.EnableVertexAttribArray(attrib);
            }

            //  Setup element buffer
            EBO = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, EBO);
            GL.BufferData(BufferTarget.ElementArrayBuffer, data.triangles.Length * sizeof(uint), data.triangles, BufferUsageHint.StaticDraw);

            //  Setup materials
            for (int i = 0; i < Materials.Count; i++)
            {
                Material m = Materials[i];

                m.Shader.Use();

                if (m.Tint != null) m.Shader.SetVec3("Tint", m.Tint.rgb);

                //  Assign texture maps
                if (m.DiffuseTexture != null)   m.Shader.SetInt("_Diffuse", 0);
                if (m.RoughnessTexture != null) m.Shader.SetInt("_Roughness", 1);
                if (m.MetallicTexture != null)  m.Shader.SetInt("_Metallic", 2);
                if (m.EmissionTexture != null)  m.Shader.SetInt("_Emission", 3);
                if (m.OcclusionTexture != null) m.Shader.SetInt("_Occlusion", 4);

                //  Fallback to PBR properties where texture maps aren't used
                if (m.RoughnessTexture == null) m.Shader.SetFloat("Roughness", m.Roughness);
                if (m.MetallicTexture == null)  m.Shader.SetFloat("Metallic", m.Metallic);
                if (m.EmissionTexture == null)  m.Shader.SetFloat("Emission", m.Emission);
            }

            boundToGL = true;

            return this;
        }

        /// <summary>
        /// Make a draw call to render this mesh using openGL
        /// <para/> This must be used within a rendering context, this is not immediate call
        /// </summary>
        internal void Render()
        {
            //  TODO Binding should ideally be done on load, not on the first render
            if (!boundToGL)
                Bind();

            if (Material != null)
            {
                GLHelper.SetProperty(EnableCap.CullFace, Material.DoubleSided);

                for (int i = 0; i < Materials.Count; i++)
                {
                    Material m = Materials[i];

                    m.DiffuseTexture?.Use(TextureUnit.Texture0);
                    m.RoughnessTexture?.Use(TextureUnit.Texture1);
                    m.MetallicTexture?.Use(TextureUnit.Texture2);
                    m.EmissionTexture?.Use(TextureUnit.Texture3);
                    m.OcclusionTexture?.Use(TextureUnit.Texture4);

                    m.Shader.Use();

                    m.Shader.SetVec2("Offset", uvOffset);
                }
            }

            GL.BindVertexArray(VAO);
            GL.DrawElements(PrimitiveType.Triangles, triangles.Length, DrawElementsType.UnsignedInt, 0);
        }
    }
}
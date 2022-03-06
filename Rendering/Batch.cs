using System;
using System.Collections.Generic;

using OpenTK.Graphics.OpenGL4;

using Swordfish.ECS;

namespace Swordfish.Rendering
{
    internal class Batch
    {
        private const int maxTriangles = 10000;
        private const int maxVertices = maxTriangles * 3;

        private List<int> Entities;

        private Material Material;

        private int VAO, VBO, EBO;

        public Batch(Material material)
        {
            Entities = new List<int>();
            Material = material;

            //  Setup vertex buffer
            VBO = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, VBO);
            GL.BufferData(BufferTarget.ArrayBuffer, maxVertices * sizeof(float), IntPtr.Zero, BufferUsageHint.DynamicDraw);

            //  Setup VAO and tell openGL how to interpret vertex data
            VAO = GL.GenVertexArray();
            GL.BindVertexArray(VAO);

            int attrib;
            attrib = material.Shader.GetAttribLocation("in_position");
            GL.VertexAttribPointer(attrib, 3, VertexAttribPointerType.Float, false, 13 * sizeof(float), 0);
            GL.EnableVertexAttribArray(attrib);

            attrib = material.Shader.GetAttribLocation("in_color");
            GL.VertexAttribPointer(attrib, 4, VertexAttribPointerType.Float, false, 13 * sizeof(float), 3 * sizeof(float));
            GL.EnableVertexAttribArray(attrib);

            attrib = material.Shader.GetAttribLocation("in_normal");
            GL.VertexAttribPointer(attrib, 3, VertexAttribPointerType.Float, false, 13 * sizeof(float), 7 * sizeof(float));
            GL.EnableVertexAttribArray(attrib);

            attrib = material.Shader.GetAttribLocation("in_uv");
            GL.VertexAttribPointer(attrib, 3, VertexAttribPointerType.Float, false, 13 * sizeof(float), 10 * sizeof(float));
            GL.EnableVertexAttribArray(attrib);

            //  Setup element buffer
            EBO = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, EBO);
            GL.BufferData(BufferTarget.ElementArrayBuffer, maxTriangles * sizeof(uint), IntPtr.Zero, BufferUsageHint.DynamicDraw);

            //  Cleanup
            GL.BindVertexArray(0);
        }

        public int[] GetEntities() => Entities.ToArray();

        public void Add(int entity) => Entities.Add(entity);

        private void BindToGL()
        {
            if (Material != null)
            {
                GLHelper.SetProperty(EnableCap.CullFace, Material.DoubleSided);

                Material.DiffuseTexture?.Use(TextureUnit.Texture0);
                Material.RoughnessTexture?.Use(TextureUnit.Texture1);
                Material.MetallicTexture?.Use(TextureUnit.Texture2);
                Material.EmissionTexture?.Use(TextureUnit.Texture3);
                Material.OcclusionTexture?.Use(TextureUnit.Texture4);

                Material.Shader.Use();

                //  TODO batch animation support
                // Material.Shader.SetVec2("Offset", uvOffset);
            }

            GL.BindVertexArray(VAO);
        }

        public void Render()
        {
            BindToGL();

            int[] counts = new int[Entities.Count];
            int[] indices = new int[Entities.Count];

            int index = 0;
            foreach (int entity in Entities)
            {
                Mesh mesh = Engine.ECS.Get<RenderComponent>(entity).mesh;

                if (mesh != null && mesh.Material != null)
                {
                    counts[index] = mesh.triangles.Length;
                    indices[index] = 0;

                    // GL.BindBuffer(BufferTarget.ArrayBuffer, mesh.VBO);
                    // GL.BindBuffer(BufferTarget.ElementArrayBuffer, mesh.EBO);
                }

                index++;
            }

            GL.MultiDrawElements(PrimitiveType.Triangles, counts, DrawElementsType.UnsignedInt, indices, counts.Length);

            GL.BindVertexArray(0);
            // GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            // GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
        }
    }
}

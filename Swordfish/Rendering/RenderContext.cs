using System;
using System.Collections.Generic;
using ImGuiNET;
using OpenTK;
using OpenTK.Mathematics;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Swordfish;
using Swordfish.Rendering.Shapes;
using Swordfish.Rendering.UI;
using System.Collections.Concurrent;
using Swordfish.ECS;

namespace Swordfish.Rendering
{
    public class RenderContext
    {
        public ImGuiController GuiController;
        private Shader shader;
        private Texture2DArray textureArray;

        private Matrix4 projection;
        private Camera camera;

        //  TODO render components
        private HashSet<Transform> renderObjects;
        private HashSet<Entity> entities;

        private float[] vertices;
        private uint[] indices;

        private int ElementBufferObject;
        private int VertexBufferObject;
        private int VertexArrayObject;

        public bool Push(Transform transform) => renderObjects.Add(transform);
        public bool Push(Entity entity) => entities.Add(entity);

        public void Load()
        {
            GL.ClearColor(0.2f, 0.3f, 0.3f, 1.0f);

            GuiController = new ImGuiController(Engine.MainWindow.ClientSize.X, Engine.MainWindow.ClientSize.Y);
            camera = new Camera(Vector3.UnitZ, Vector3.Zero);

            renderObjects = new HashSet<Transform>();
            entities = new HashSet<Entity>();

            MeshData mesh = (new Cube()).GetRawData();
            vertices = mesh.vertices;
            indices = mesh.triangles;

            //  Shaders
            shader = new Shader("shaders/testArray.vert", "shaders/testArray.frag", "Test");
            shader.Use();

            //  Setup vertex buffer
            VertexBufferObject = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, VertexBufferObject);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);

            //  Setup VAO and tell openGL how to interpret vertex data
            VertexArrayObject = GL.GenVertexArray();
            GL.BindVertexArray(VertexArrayObject);

            int attrib = shader.GetAttribLocation("in_position");
            GL.VertexAttribPointer(attrib, 3, VertexAttribPointerType.Float, false, 13 * sizeof(float), 0);
            GL.EnableVertexAttribArray(attrib);

            attrib = shader.GetAttribLocation("in_color");
            GL.VertexAttribPointer(attrib, 4, VertexAttribPointerType.Float, false, 13 * sizeof(float), 3 * sizeof(float));
            GL.EnableVertexAttribArray(attrib);

            attrib = shader.GetAttribLocation("in_uv");
            GL.VertexAttribPointer(attrib, 3, VertexAttribPointerType.Float, false, 13 * sizeof(float), 10 * sizeof(float));
            GL.EnableVertexAttribArray(attrib);

            //  Setup element buffer
            ElementBufferObject = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, ElementBufferObject);
            GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(uint), indices, BufferUsageHint.StaticDraw);

            //  Textures
            textureArray = Texture2DArray.CreateFromFolder("resources/textures/block/", "blocks");
            textureArray.SetMinFilter(TextureMinFilter.Nearest);
            textureArray.SetMagFilter(TextureMagFilter.Nearest);
            textureArray.SetWrap(TextureCoordinate.S, TextureWrapMode.ClampToEdge);
            textureArray.Use(TextureUnit.Texture0);
            shader.SetInt("texture0", 0);
        }

        public void Unload()
        {
            //  Unbind resources
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindVertexArray(0);
            GL.UseProgram(0);

            // Delete resources
            GL.DeleteBuffer(ElementBufferObject);
            GL.DeleteBuffer(VertexBufferObject);
            GL.DeleteVertexArray(VertexArrayObject);
            GL.DeleteProgram(shader.Handle);

            //  Dispose shaders
            shader.Dispose();
        }

        public void Render()
        {
            //  Clear the buffer, enable depth testing, enable backface culling
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.CullFace);

            shader.Use();
            textureArray.Use(TextureUnit.Texture0);

            camera.Update();

            projection = Matrix4.CreatePerspectiveFieldOfView(
                    MathHelper.DegreesToRadians(camera.FOV),
                    (float)Engine.MainWindow.ClientSize.X / (float)Engine.MainWindow.ClientSize.Y,
                    Engine.Settings.CLIP_NEAR,
                    Engine.Settings.CLIP_FAR
                );

            shader.SetMatrix4("view", camera.view);
            shader.SetMatrix4("projection", projection);

            //  Make a draw call per object
            //  TODO: batching
            //  TODO: this just draws cubes currently
            GL.BindVertexArray(VertexArrayObject);

            Matrix4 transformMatrix;

            foreach (Entity entity in entities)
            {
                if (entity.HasComponents(typeof(ECSTest.PositionComponent), typeof(ECSTest.RotationComponent), typeof(ECSTest.RenderComponent)))
                {
                    transformMatrix = Matrix4.CreateFromQuaternion(entity.GetData<ECSTest.RotationComponent>().orientation)
                                    * Matrix4.CreateTranslation(entity.GetData<ECSTest.PositionComponent>().position);

                    shader.SetMatrix4("transform", transformMatrix);

                    GL.DrawElements(PrimitiveType.Triangles, indices.Length, DrawElementsType.UnsignedInt, 0);
                }
            }

            // foreach (Transform transform in renderObjects)
            // {
            //     shader.SetMatrix4("transform", transform.GetMatrix());

            //     GL.DrawElements(PrimitiveType.Triangles, indices.Length, DrawElementsType.UnsignedInt, 0);
            // }

            //  Draw GUI elements
            //  Disable depth testing for this pass
            GL.Disable(EnableCap.DepthTest);
            GuiController.Update(Engine.MainWindow, Engine.DeltaTime);
                if (Debug.Enabled) Debug.ShowDebugGui();
                else if (Debug.Stats) Debug.ShowStatsGui();

                //  Invoke callback
                Engine.GuiCallback?.Invoke();
            GuiController.Render();
        }
    }
}
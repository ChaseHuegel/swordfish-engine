using System;
using System.Collections.Generic;
using System.Linq;
using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using source;
using Swordfish.Diagnostics;
using Swordfish.ECS;
using Swordfish.Rendering.Shapes;
using Swordfish.Rendering.UI;
using Swordfish.Types;
using Swordfish.Util;

namespace Swordfish.Rendering
{
    public class RenderContext
    {
        public ImGuiController GuiController;
        private Shader shader;
        private Texture2DArray textureArray;

        private Matrix4 projection;
        private Camera camera;

        private int[] entities;

        private float[] vertices;
        private uint[] indices;

        private int ElementBufferObject;
        private int VertexBufferObject;
        private int VertexArrayObject;

        public int DrawCalls = 0;

        /// <summary>
        /// Push all entities to context that should be rendered each frame
        /// </summary>
        /// <param name="entities"></param>
        internal void Push(int[] entities) => this.entities = entities;

        /// <summary>
        /// Load the renderer
        /// </summary>
        public void Load()
        {
            Debug.Log($"OpenGL v{GL.GetString(StringName.Version)}");
            Debug.Log($"    {GL.GetString(StringName.Vendor)} {GL.GetString(StringName.Renderer)}");
            Debug.Log($"    Extensions found: {GLHelper.GetSupportedExtensions().Count}");
            GL.GetInteger(GetPName.MaxVertexAttribs, out int maxAttributeCount);
            Debug.Log($"    Shader vertex attr supported: {maxAttributeCount}");

            Debug.TryCreateGLOutput();

            GL.ClearColor(0.2f, 0.3f, 0.3f, 1.0f);

            GuiController = new ImGuiController(Engine.MainWindow.ClientSize.X, Engine.MainWindow.ClientSize.Y);
            camera = new Camera(Vector3.Zero, Vector3.Zero);

            entities = new int[0];

            MeshData mesh = (new Cube()).GetRawData();
            vertices = mesh.vertices;
            indices = mesh.triangles;

            //  Shaders
            shader = Shader.LoadFromFile("shaders/testArray.vert", "shaders/testArray.frag", "TestArray");
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

            attrib = shader.GetAttribLocation("in_normal");
            GL.VertexAttribPointer(attrib, 4, VertexAttribPointerType.Float, false, 13 * sizeof(float), 7 * sizeof(float));
            GL.EnableVertexAttribArray(attrib);

            attrib = shader.GetAttribLocation("in_uv");
            GL.VertexAttribPointer(attrib, 3, VertexAttribPointerType.Float, false, 13 * sizeof(float), 10 * sizeof(float));
            GL.EnableVertexAttribArray(attrib);

            //  Setup element buffer
            ElementBufferObject = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, ElementBufferObject);
            GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(uint), indices, BufferUsageHint.StaticDraw);

            //  Textures
            textureArray = Texture2DArray.LoadFromFolder("resources/textures/block/", "blocks");
        }

        /// <summary>
        /// Unload the renderer
        /// </summary>
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

        /// <summary>
        /// Render the context
        /// </summary>
        public void Render()
        {
            //  Clear the buffer, enable depth testing, enable backface culling
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.Enable(EnableCap.DepthTest);

            //  Alpha blending
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            camera.Update();

            projection = Matrix4.CreatePerspectiveFieldOfView(
                    MathHelper.DegreesToRadians(camera.FOV),
                    (float)Engine.MainWindow.ClientSize.X / (float)Engine.MainWindow.ClientSize.Y,
                    Engine.Settings.Renderer.CLIP_NEAR,
                    Engine.Settings.Renderer.CLIP_FAR
                );

            //  TODO: batching
            //  TODO as part of batching, only sort entities that are actually using transparency shaders

            //  Sort for transparency
            SortedDictionary<float, Entity> sortedEntities = new SortedDictionary<float, Entity>();
            foreach (Entity entity in entities)
            {
                Vector3 point = Engine.ECS.Get<PositionComponent>(entity).position;
                Vector3 origin = camera.transform.position - camera.transform.forward;

                //  Greedily cull entities beyond the far clip plane
                if (!Intersection.BoundingToPoint(origin, Engine.Settings.Renderer.CLIP_FAR, point))
                    continue;

                //  ...Build a frustrum for culling
                Plane[] planes = Plane.BuildViewFrustrum(
                        origin,
                        camera.transform.forward,
                        camera.transform.up,
                        camera.transform.right,
                        camera.FOV,
                        Engine.Settings.Renderer.CLIP_NEAR,
                        Engine.Settings.Renderer.CLIP_FAR
                    );

                //  Check the frustrum
                if (!Intersection.FrustrumToPoint(planes, point))
                    continue;

                //  ... Not culled, sort the entity by distance
                float distance = MathS.DistanceUnsquared(camera.transform.position, Engine.ECS.Get<PositionComponent>(entity).position);
                sortedEntities[distance] = entity;
            }

            DrawCalls = 0;
            Matrix4 transformMatrix;
            foreach (KeyValuePair<float, Entity> pair in sortedEntities.Reverse())
            {
                //  Make a draw call per entity
                Entity entity = pair.Value;

                transformMatrix = Matrix4.CreateFromQuaternion(Engine.ECS.Get<RotationComponent>(entity).orientation)
                                    * Matrix4.CreateTranslation(Engine.ECS.Get<PositionComponent>(entity).position);

                Mesh mesh = Engine.ECS.Get<RenderComponent>(entity).mesh;
                if (mesh != null)
                {
                    mesh.Shader.SetMatrix4("view", camera.view);
                    mesh.Shader.SetMatrix4("projection", projection);
                    mesh.Shader.SetMatrix4("transform", transformMatrix);
                    mesh.Shader.SetMatrix4("inversedTransform", transformMatrix.Inverted());

                    mesh.Shader.SetVec3("globalLightDirection", new Vector3(45, 0, 0));
                    mesh.Shader.SetVec4("globalLightColor", Color.White);

                    mesh.Shader.SetFloat("ambientLightning", 0.25f);
                    mesh.Shader.SetVec4("ambientLightColor", Color.Gray);
                    mesh.Render();
                }
                else
                {
                    shader.SetMatrix4("view", camera.view);
                    shader.SetMatrix4("projection", projection);
                    shader.SetMatrix4("transform", transformMatrix);
                    shader.SetMatrix4("inversedTransform", transformMatrix.Inverted());

                    shader.SetVec3("globalLightDirection", new Vector3(45, 0, 0));
                    shader.SetVec4("globalLightColor", Color.White);

                    shader.SetFloat("ambientLightning", 0.25f);
                    shader.SetVec4("ambientLightColor", Color.Gray);

                    shader.Use();
                    textureArray.Use(TextureUnit.Texture0);

                    GL.BindVertexArray(VertexArrayObject);

                    //  TODO temporary physics visual debug
                    if (Engine.ECS.Get<CollisionComponent>(entity).colliding)
                        shader.SetVec4("tint", Color.Red);
                    else if (Engine.ECS.Get<CollisionComponent>(entity).broadHit)
                        shader.SetVec4("tint", Color.Blue);
                    else
                        shader.SetVec4("tint", Color.White);

                    GL.DrawElements(PrimitiveType.Triangles, indices.Length, DrawElementsType.UnsignedInt, 0);
                }

                DrawCalls++;
                GL.BindVertexArray(0);
            }

            //  Draw GUI elements
            //  Disable depth testing for this pass
            GL.Disable(EnableCap.DepthTest);
            GuiController.Update(Engine.MainWindow, Engine.DeltaTime);

                //  Try presenting debug
                if (Debug.Enabled) Debug.ShowGui();

                //  Try presenting the console
                if (Debug.Console) Logger.ShowGui();

                //  Invoke GUI callback
                Engine.GuiCallback?.Invoke();
            GuiController.Render();
        }
    }
}
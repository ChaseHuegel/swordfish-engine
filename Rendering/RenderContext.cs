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
        public int DrawCalls = 0;

        private Shader postprocessing;
        private int RenderTexture;
        private int FrameBufferObject;
        private int RenderBufferObject;

        Vector4 clearColor = new Vector4(0.08f, 0.1f, 0.14f, 1.0f);

        public ImGuiController GuiController;
        private Shader shader;
        private Texture2DArray textureArray;

        private Matrix4 projection;
        private Camera camera;

        private int[] entities;
        private int[] lights;

        private float[] vertices;
        private uint[] indices;

        private int ElementBufferObject;
        private int VertexBufferObject;
        private int VertexArrayObject;

        private const int MAX_LIGHTS = 4;

        /// <summary>
        /// Push all entities to context that should be rendered each frame
        /// </summary>
        /// <param name="entities"></param>
        internal void Push(int[] entities) => this.entities = entities;

        /// <summary>
        /// Push all entities that act as light sources
        /// </summary>
        /// <param name="entities"></param>
        internal void PushLights(int[] entities) => this.lights = entities;

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

            GuiController = new ImGuiController(Engine.MainWindow.ClientSize.X, Engine.MainWindow.ClientSize.Y);
            camera = new Camera(Vector3.Zero, Vector3.Zero);

            entities = new int[0];
            lights = new int[0];

            MeshData mesh = (new Cube()).GetRawData();
            vertices = mesh.vertices;
            indices = mesh.triangles;

            //  Shaders
            shader = Shaders.PBR_ARRAY.Get();
            shader.Use();
            postprocessing = Shaders.POSTPROCESSING.Get();
            postprocessing.Use();
            postprocessing.SetInt("texture0", 0);

            //  Setup framebuffer
            FrameBufferObject = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, FrameBufferObject);
            GL.Viewport(0, 0, Engine.MainWindow.ClientSize.X, Engine.MainWindow.ClientSize.Y);

            //  Render texture
            RenderTexture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, RenderTexture);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba,
                        Engine.MainWindow.ClientSize.X, Engine.MainWindow.ClientSize.Y, 0,
                        PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, RenderTexture, 0);

            //  Render buffer for depth and stencil
            RenderBufferObject = GL.GenRenderbuffer();
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, RenderBufferObject);
            GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.Depth24Stencil8, Engine.MainWindow.ClientSize.X, Engine.MainWindow.ClientSize.Y);
            GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment, RenderbufferTarget.Renderbuffer, RenderBufferObject);
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0);

            //  Check if framebuffer is completed
            FramebufferErrorCode framebufferCode = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (framebufferCode != FramebufferErrorCode.FramebufferComplete)
                Debug.Log($"Framebuffer code: {framebufferCode.ToString()}", LogType.ERROR);
            else
                Debug.Log($"Framebuffer code: {framebufferCode.ToString()}");

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);   //  Bind back to default buffer

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
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, FrameBufferObject);

            //  Clear the buffer, enable depth testing, enable backface culling
            Vector4 clr = clearColor;   //  convert from linear colorspace
            clr.X = (float)Math.Pow(clr.X, 2.2f);
            clr.Y = (float)Math.Pow(clr.Y, 2.2f);
            clr.Z = (float)Math.Pow(clr.Z, 2.2f);
            GL.ClearColor(clr.X, clr.Y, clr.Z, 1f);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.Enable(EnableCap.DepthTest);

            //  Alpha blending
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            //  Wireframe
            // GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);

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

                if (Engine.ECS.HasComponent<RotationComponent>(entity))
                    transformMatrix = Matrix4.CreateFromQuaternion(Engine.ECS.Get<RotationComponent>(entity).orientation)
                                    * Matrix4.CreateTranslation(Engine.ECS.Get<PositionComponent>(entity).position);
                else
                    transformMatrix = Matrix4.CreateTranslation(Engine.ECS.Get<PositionComponent>(entity).position);

                Mesh mesh = Engine.ECS.Get<RenderComponent>(entity).mesh;
                if (mesh != null)
                {
                    mesh.Shader.SetMatrix4("view", camera.view);
                    mesh.Shader.SetMatrix4("projection", projection);
                    mesh.Shader.SetMatrix4("transform", transformMatrix);
                    mesh.Shader.SetMatrix4("inversedTransform", transformMatrix.Inverted());

                    mesh.Shader.SetVec3("viewPosition", camera.transform.position);

                    mesh.Shader.SetFloat("ambientLightning", 0.03f);

                    mesh.Shader.SetFloat("ao", 1f);
                    mesh.Shader.SetFloat("metallic", 0.5f);
                    mesh.Shader.SetFloat("roughness", 0.5f);

                    for (int i = 0; i < lights.Length; i++)
                    {
                        mesh.Shader.SetVec3($"lightPositions[{i}]", Engine.ECS.Get<PositionComponent>(lights[i]).position);
                        mesh.Shader.SetVec3($"lightColors[{i}]", Engine.ECS.Get<LightComponent>(lights[i]).color.Xyz * Engine.ECS.Get<LightComponent>(lights[i]).intensity);
                        mesh.Shader.SetFloat($"lightRanges[{i}]", Engine.ECS.Get<LightComponent>(lights[i]).range);
                    }

                    if (lights.Length < MAX_LIGHTS)
                    {
                        for (int i = lights.Length; i < MAX_LIGHTS; i++)
                        {
                            mesh.Shader.SetVec3($"lightPositions[{i}]", Vector3.Zero);
                            mesh.Shader.SetVec3($"lightColors[{i}]", Color.Black.Xyz);
                            mesh.Shader.SetFloat($"lightRanges[{i}]", 0f);
                        }
                    }

                    mesh.Render();
                }
                else
                {
                    shader.SetMatrix4("view", camera.view);
                    shader.SetMatrix4("projection", projection);
                    shader.SetMatrix4("transform", transformMatrix);
                    shader.SetMatrix4("inversedTransform", transformMatrix.Inverted());

                    shader.SetVec3("viewPosition", camera.transform.position);

                    shader.SetFloat("ambientLightning", 0.03f);

                    shader.SetFloat("ao", 1f);
                    shader.SetFloat("metallic", 0.5f);
                    shader.SetFloat("roughness", 0.5f);

                    for (int i = 0; i < lights.Length; i++)
                    {
                        shader.SetVec3($"lightPositions[{i}]", Engine.ECS.Get<PositionComponent>(lights[i]).position);
                        shader.SetVec3($"lightColors[{i}]", Engine.ECS.Get<LightComponent>(lights[i]).color.Xyz * Engine.ECS.Get<LightComponent>(lights[i]).intensity);
                        shader.SetFloat($"lightRanges[{i}]", Engine.ECS.Get<LightComponent>(lights[i]).range);
                    }

                    if (lights.Length < MAX_LIGHTS)
                    {
                        for (int i = lights.Length; i < MAX_LIGHTS; i++)
                        {
                            shader.SetVec3($"lightPositions[{i}]", Vector3.Zero);
                            shader.SetVec3($"lightColors[{i}]", Color.Black.Xyz);
                            shader.SetFloat($"lightRanges[{i}]", 0f);
                        }
                    }

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

            //  Disable depth testing for this pass
            GL.Disable(EnableCap.DepthTest);
            GL.Disable(EnableCap.CullFace);

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.ClearColor(1f, 1f, 1f, 1.0f);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            //  Draw post processing
            // postprocessing.Use();
            // GL.BindVertexArray(screenVAO);
            // GL.ActiveTexture(TextureUnit.Texture0);
            // GL.BindTexture(TextureTarget.Texture2D, RenderTexture);
            // GL.DrawArrays(PrimitiveType.Triangles, 0, 6);

            //  ! TODO This is extremely hacky but it got the framebuffer texture rendering
            //  ! Cleaning up and putting together a proper implementation ASAP
            //  ! This is also creating some openGL error for InvalidValue
            Mesh m = new Quad();
            m.Scale = Vector3.One * 2f;
            m.Shader = postprocessing;
            m.Texture = new Texture2D(RenderTexture, "", Engine.MainWindow.ClientSize.X, Engine.MainWindow.ClientSize.Y, false);
            m.Bind();
            m.Render();

            //  Draw GUI elements
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
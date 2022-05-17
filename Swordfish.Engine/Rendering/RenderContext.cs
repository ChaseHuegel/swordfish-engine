using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;

using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

using Swordfish.Library.Diagnostics;
using Swordfish.Engine.ECS;
using Swordfish.Library.Extensions;
using Swordfish.Engine.Rendering.Shapes;
using Swordfish.Engine.Rendering.UI;
using Swordfish.Library.Types;
using Swordfish.Library.Util;

using Color = Swordfish.Library.Types.Color;
using PixelFormat = OpenTK.Graphics.OpenGL4.PixelFormat;
using Swordfish.Engine.Rendering.UI.Elements.Diagnostics;

namespace Swordfish.Engine.Rendering
{
    public class RenderContext
    {
        public int DrawCalls = 0;

        private int RenderTexture;
        private int HdrTexture;
        private int FrameBufferObject;
        private int RenderBufferObject;

        private Mesh renderTarget;
        private float exposureChange = 0f;
        private float lastExposure = 0f;

        public ImGuiController GuiController;
        public UiContext UiContext;

        private ProfilerWindow profilerWindow;
        private StatsWindow statsWindow;
        private ConsoleWindow consoleWindow;
        
        private Shader shader;
        private Texture2DArray textureArray;
        private Texture2D hdrTexture;

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
        /// Update the renderer to use the current window size
        /// </summary>
        public void OnWindowResized() => Reload();

        /// <summary>
        /// Takes a screenshot of the entire window
        /// <para/> Pass false to only capture the render target
        /// </summary>
        /// <param name="wholeScreen">true to capture the entire window; false captures the render target</param>
        /// <returns>bitmap representing the screenshot</returns>
        public Bitmap Screenshot(bool wholeScreen = true)
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, wholeScreen ? 0 : FrameBufferObject);

            Bitmap bitmap = new Bitmap(Swordfish.MainWindow.ClientSize.X, Swordfish.MainWindow.ClientSize.Y);
            BitmapData data = bitmap.LockBits(new Rectangle(0, 0, Swordfish.MainWindow.ClientSize.X, Swordfish.MainWindow.ClientSize.Y),
                                ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            GL.ReadPixels(0, 0, Swordfish.MainWindow.ClientSize.X, Swordfish.MainWindow.ClientSize.Y,
                        PixelFormat.Bgra, PixelType.UnsignedByte, data.Scan0);

            bitmap.UnlockBits(data);
            bitmap.RotateFlip(RotateFlipType.RotateNoneFlipY);  //  Flip the image, openGL is upside down

            //  Perform gamma correction if capturing the render target; gamma correction is normally done in post
            if (!wholeScreen)
                bitmap.SetGamma(2.2f);

            return bitmap;
        }

        /// <summary>
        /// Initialize the renderer
        /// </summary>
        public void Initialize()
        {
            Debug.Log($"OpenGL v{GL.GetString(StringName.Version)}");
            Debug.Log($"{GL.GetString(StringName.Vendor)} {GL.GetString(StringName.Renderer)}", LogType.CONTINUED);
            Debug.Log($"Extensions found: {GLHelper.GetSupportedExtensions().Count}", LogType.CONTINUED);

            GL.GetInteger(GetPName.MaxVertexAttribs, out int maxAttributeCount);
            Debug.Log($"Shader vertex attr supported: {maxAttributeCount}", LogType.CONTINUED);

            Debug.TryCreateGLOutput();

            GuiController = new ImGuiController(Swordfish.MainWindow.ClientSize.X, Swordfish.MainWindow.ClientSize.Y);
            UiContext = new UiContext();

            //  Setup debug UI
            profilerWindow = new ProfilerWindow();
            statsWindow = new StatsWindow();
            consoleWindow = new ConsoleWindow();
            
            camera = new Camera(Vector3.Zero, Vector3.Zero);

            entities = new int[0];
            lights = new int[0];

            MeshData mesh = (new Cube()).GetRawData();
            vertices = mesh.vertices;
            indices = mesh.triangles;

            Load();
        }

        /// <summary>
        /// Reload the renderer
        /// </summary>
        public void Reload()
        {
            //  TODO #28 unloading breaks rendering hardcoded test cubes
            // Unload();
            Load();

            GuiController.OnWindowResized(Swordfish.MainWindow.ClientSize.X, Swordfish.MainWindow.ClientSize.Y);
        }

        /// <summary>
        /// Load the renderer
        /// </summary>
        private void Load()
        {
            //  Shaders
            shader = Shaders.PBR_ARRAY.Get();
            shader.Use();

            //  Setup framebuffer
            FrameBufferObject = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, FrameBufferObject);
            GL.Viewport(0, 0, Swordfish.MainWindow.ClientSize.X, Swordfish.MainWindow.ClientSize.Y);

            //  Render texture
            RenderTexture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, RenderTexture);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba16f,
                        Swordfish.MainWindow.ClientSize.X, Swordfish.MainWindow.ClientSize.Y, 0,
                        PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, RenderTexture, 0);

            int mipmapLevels = (byte)Math.Floor(Math.Log(Math.Max(Swordfish.MainWindow.ClientSize.X, Swordfish.MainWindow.ClientSize.Y), 2));
            GL.TextureStorage2D(RenderTexture, mipmapLevels, SizedInternalFormat.Rgba16f, Swordfish.MainWindow.ClientSize.X, Swordfish.MainWindow.ClientSize.Y);
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
            GL.TextureParameter(RenderTexture, TextureParameterName.TextureMaxLevel, mipmapLevels - 1);

            //  HDR render texture
            HdrTexture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, HdrTexture);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba16f,
                        Swordfish.MainWindow.ClientSize.X, Swordfish.MainWindow.ClientSize.Y, 0,
                        PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment1, TextureTarget.Texture2D, HdrTexture, 0);

            //  Use 2 color attachments
            GL.DrawBuffers(2, new DrawBuffersEnum[] { DrawBuffersEnum.ColorAttachment0, DrawBuffersEnum.ColorAttachment1 });

            //  Render buffer for depth and stencil
            RenderBufferObject = GL.GenRenderbuffer();
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, RenderBufferObject);
            GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.Depth24Stencil8, Swordfish.MainWindow.ClientSize.X, Swordfish.MainWindow.ClientSize.Y);
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
            textureArray = Texture2DArray.LoadFromFolder($"{Directories.TEXTURES}/block/", "blocks");

            hdrTexture = new Texture2D(HdrTexture,
                         "HDR Texture",
                         Swordfish.MainWindow.ClientSize.X,
                         Swordfish.MainWindow.ClientSize.Y,
                         false);

            //  Render texture
            renderTarget = new Quad();
            renderTarget.Scale = Vector3.One * 2f;

            renderTarget.Material = new Material()
            {
                Name = "Render Target",
                Shader = Shaders.POST.Get(),
                DiffuseTexture = new Texture2D(RenderTexture,
                                 "Render Texture",
                                 Swordfish.MainWindow.ClientSize.X,
                                 Swordfish.MainWindow.ClientSize.Y,
                                 false)
            };
        }

        /// <summary>
        /// Disposes resources used by the renderer
        /// </summary>
        public void Dispose() => Unload();

        /// <summary>
        /// Unload the renderer
        /// </summary>
        private void Unload()
        {
            // Delete resources
            GL.DeleteBuffer(FrameBufferObject);
            GL.DeleteBuffer(ElementBufferObject);
            GL.DeleteBuffer(VertexBufferObject);
            GL.DeleteVertexArray(VertexArrayObject);
            GL.DeleteProgram(shader.Handle);

            //  Dispose shaders
            shader.Dispose();

            //  Dispose gui controller
            GuiController.Dispose();
        }

        /// <summary>
        /// Render the context
        /// </summary>
        public void Render()
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, FrameBufferObject);

            //  Clear the buffer, enable depth testing, enable culling
            GL.ClearColor(0f, 0f, 0f, 0f);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            GL.Enable(EnableCap.DepthTest);

            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);

            //  Alpha blending
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            //  Wireframe
            if (Swordfish.Settings.Renderer.WIREFRAME)
                GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
            else
                GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);

            camera.Update();

            projection = Matrix4.CreatePerspectiveFieldOfView(
                    MathHelper.DegreesToRadians(camera.FOV),
                    (float)Swordfish.MainWindow.ClientSize.X / (float)Swordfish.MainWindow.ClientSize.Y,
                    Swordfish.Settings.Renderer.CLIP_NEAR,
                    Swordfish.Settings.Renderer.CLIP_FAR
                );

            //  TODO: batching
            //  TODO as part of batching, only sort entities that are actually using transparency shaders

            //  Sort for transparency
            SortedDictionary<float, Entity> sortedEntities = new SortedDictionary<float, Entity>();
            foreach (Entity entity in entities)
            {
                Vector3 point = Swordfish.ECS.Get<TransformComponent>(entity).position;
                Vector3 origin = camera.transform.position - camera.transform.forward;

                //  Greedily cull entities beyond the far clip plane
                if (!Intersection.BoundingToPoint(origin, Swordfish.Settings.Renderer.CLIP_FAR, point))
                    continue;

                //  ...Build a frustrum for culling
                Plane[] planes = Plane.BuildViewFrustrum(
                        origin,
                        camera.transform.forward,
                        camera.transform.up,
                        camera.transform.right,
                        camera.FOV,
                        Swordfish.Settings.Renderer.CLIP_NEAR,
                        Swordfish.Settings.Renderer.CLIP_FAR
                    );

                //  Check the frustrum
                if (!Intersection.FrustrumToPoint(planes, point))
                    continue;

                //  ... Not culled, sort the entity by distance
                float distance = MathS.DistanceUnsquared(camera.transform.position, Swordfish.ECS.Get<TransformComponent>(entity).position);
                sortedEntities[distance] = entity;
            }

            DrawCalls = 0;
            Matrix4 transformMatrix;
            foreach (KeyValuePair<float, Entity> pair in sortedEntities.Reverse())
            {
                //  Make a draw call per entity
                Entity entity = pair.Value;

                transformMatrix = Matrix4.CreateFromQuaternion(Swordfish.ECS.Get<TransformComponent>(entity).orientation)
                                * Matrix4.CreateTranslation(Swordfish.ECS.Get<TransformComponent>(entity).position);

                Mesh mesh = Swordfish.ECS.Get<RenderComponent>(entity).mesh;
                if (mesh != null && mesh.Material != null)
                {
                    foreach (Material m in mesh.Materials)
                    {
                        Shader shader = m.Shader;

                        shader.SetMatrix4("view", camera.view);
                        shader.SetMatrix4("projection", projection);
                        shader.SetMatrix4("transform", transformMatrix);
                        shader.SetMatrix4("inversedTransform", transformMatrix.Inverted());

                        shader.SetVec3("viewPosition", camera.transform.position);

                        shader.SetFloat("ambientLightning", 0.03f);

                        shader.SetFloat("Metallic", m.Metallic);
                        shader.SetFloat("Roughness", m.Roughness);

                        for (int i = 0; i < lights.Length; i++)
                        {
                            shader.SetVec3($"lightPositions[{i}]", Swordfish.ECS.Get<TransformComponent>(lights[i]).position);
                            shader.SetVec3($"lightColors[{i}]", Swordfish.ECS.Get<LightComponent>(lights[i]).color.Xyz * Swordfish.ECS.Get<LightComponent>(lights[i]).lumens);
                        }

                        if (lights.Length < MAX_LIGHTS)
                        {
                            for (int i = lights.Length; i < MAX_LIGHTS; i++)
                            {
                                shader.SetVec3($"lightPositions[{i}]", Vector3.Zero);
                                shader.SetVec3($"lightColors[{i}]", Color.Black.rgb);
                            }
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

                    shader.SetFloat("Metallic", 0f);
                    shader.SetFloat("Roughness", 1f);

                    for (int i = 0; i < lights.Length; i++)
                    {
                        shader.SetVec3($"lightPositions[{i}]", Swordfish.ECS.Get<TransformComponent>(lights[i]).position);
                        shader.SetVec3($"lightColors[{i}]", Swordfish.ECS.Get<LightComponent>(lights[i]).color.Xyz * Swordfish.ECS.Get<LightComponent>(lights[i]).lumens);
                    }

                    if (lights.Length < MAX_LIGHTS)
                    {
                        for (int i = lights.Length; i < MAX_LIGHTS; i++)
                        {
                            shader.SetVec3($"lightPositions[{i}]", Vector3.Zero);
                            shader.SetVec3($"lightColors[{i}]", Color.Black.rgb);
                        }
                    }

                    shader.Use();
                    textureArray.Use(TextureUnit.Texture0);

                    if (mesh != null)
                        mesh.Render();
                    else
                    {
                        GL.BindVertexArray(VertexArrayObject);

                        //  TODO temporary physics visual debug
                        if (Swordfish.ECS.Get<CollisionComponent>(entity).colliding)
                            shader.SetVec4("tint", Color.Red);
                        else if (Swordfish.ECS.Get<CollisionComponent>(entity).broadHit)
                            shader.SetVec4("tint", Color.Blue);
                        else
                            shader.SetVec4("tint", Color.White);

                        GL.DrawElements(PrimitiveType.Triangles, indices.Length, DrawElementsType.UnsignedInt, 0);
                    }
                }

                DrawCalls++;
                GL.BindVertexArray(0);
            }

            //  Read highest luminance at center of screen
            int pixelCount = 128*128;
            byte[] pixels = new byte[3*pixelCount];
            GL.ReadPixels((Swordfish.MainWindow.ClientSize.X/2)-64, (Swordfish.MainWindow.ClientSize.Y/2)-64, 128, 128, PixelFormat.Rgb, PixelType.UnsignedByte, pixels);
            float luminance = 0f;
            for (int n = 0; n < pixelCount; n++)
            {
                Color c = new Color(pixels[n], pixels[n + 1], pixels[n + 2], 1f);

                //  Gamma correction for better accuracy
                c.r = (float)Math.Pow(c.r, 1f / 2.2f);
                c.g = (float)Math.Pow(c.g, 1f / 2.2f);
                c.b = (float)Math.Pow(c.b, 1f / 2.2f);

                float lum = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

                //  only consider luminance above a threshold
                if (lum > 10f && lum > luminance) luminance = lum-10f;
            }

            //  -----------------------------------------------------
            //  --- Second render pass for overlays and post processing ---

            //  If wireframe is enabled, ensure we aren't rendering overlays in wireframe
            if (Swordfish.Settings.Renderer.WIREFRAME)
                GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);

            //  Disable depth testing and culling for this pass
            GL.Disable(EnableCap.DepthTest);
            GL.Disable(EnableCap.CullFace);

            //  Bind and clear
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.ClearColor(0f, 0f, 0f, 1f);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            renderTarget.Material.DiffuseTexture.Use(TextureUnit.Texture0);
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);

            //  TODO Very messy auto exposure code
            float e = 0;
            float exposure = Swordfish.Settings.Renderer.EXPOSURE;
            for (int i = 0; i < lights.Length; i++)
            {
                Vector3 relative = Swordfish.ECS.Get<TransformComponent>(lights[i]).position - Camera.Main.transform.position;
                relative.NormalizeFast();

                float dot = Vector3.Dot(relative, Camera.Main.transform.forward);

                float distance = Vector3.Distance(Camera.Main.transform.position, Swordfish.ECS.Get<TransformComponent>(lights[i]).position);
                distance /= 70f/Camera.Main.FOV;

                float facing = MathS.RangeToRange(dot, -1f, 1f, distance+1f, 1f);

                float attenuation = 1f/(distance*distance);

                e += Swordfish.ECS.Get<LightComponent>(lights[i]).lumens * (float)Math.Pow(attenuation, facing);
            }
            e /= lights.Length;
            e = 10f / (e * luminance);
            e = Math.Clamp(e, 0.2f, 1.5f);

            if (lastExposure != exposure) exposureChange = 0f;
            exposure = MathS.Slerp(exposure, e, exposureChange);
            exposureChange += 0.02f* Swordfish.DeltaTime;
            Swordfish.Settings.Renderer.EXPOSURE = exposure;
            lastExposure = exposure;

            //  Display the render texture onto the screen
            renderTarget.Material.Shader.SetVec4("BackgroundColor", Swordfish.Settings.Renderer.BACKGROUND_COLOR);

            //  ! Disabled auto exposure
            // renderTarget.Material.Shader.SetFloat("Exposure", exposure);

            hdrTexture.Use(TextureUnit.Texture1);
            renderTarget.Render();

            //  Draw GUI elements
            GuiController.Update(Swordfish.MainWindow, Swordfish.DeltaTime);
                UiContext.Render();

            //  Invoke GUI callback
            Swordfish.GuiCallback?.Invoke();
            GuiController.Render();
            //  -----------------------------------------------------
        }
    }
}
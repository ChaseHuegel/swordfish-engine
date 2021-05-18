using System;
using ImGuiNET;
using OpenTK;
using OpenTK.Mathematics;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Swordfish;
using Swordfish.Rendering;
using Swordfish.Rendering.Shapes;
using System.Drawing.Drawing2D;

namespace waywardbeyond
{
    public class Game : GameWindow
    {
        private ImGuiController guiController;
        private Shader shader;
        private Texture2DArray textureArray;

        private Matrix4 projection;
        private Camera camera;
        private float cameraSpeed = 12f;

        private float degrees;

        private float[] vertices;
        private uint[] indices;

        private int ElementBufferObject;
        private int VertexBufferObject;
        private int VertexArrayObject;

        public float DeltaTime = 0f;

        public Game(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings) : base(gameWindowSettings, nativeWindowSettings)
        {
        }

        // This function runs on every update frame.
        protected override void OnUpdateFrame(FrameEventArgs e)
        {
            //  Don't update if the window isn't in focus
            if (!IsFocused) return;

            if (KeyboardState.IsKeyDown(Keys.Escape))
                Close();

            if (KeyboardState.IsKeyDown(Keys.W))
                camera.transform.position += camera.transform.forward * cameraSpeed * (float)e.Time;
            if (KeyboardState.IsKeyDown(Keys.S))
                camera.transform.position -= camera.transform.forward * cameraSpeed * (float)e.Time;

            if (KeyboardState.IsKeyDown(Keys.A))
                camera.transform.position += camera.transform.right * cameraSpeed * (float)e.Time;
            if (KeyboardState.IsKeyDown(Keys.D))
                camera.transform.position -= camera.transform.right * cameraSpeed * (float)e.Time;

            if (KeyboardState.IsKeyDown(Keys.Space))
                camera.transform.position += camera.transform.up * cameraSpeed * (float)e.Time;
            if (KeyboardState.IsKeyDown(Keys.LeftControl))
                camera.transform.position -= camera.transform.up * cameraSpeed * (float)e.Time;

            if (KeyboardState.IsKeyPressed(Keys.C))
                camera.FOV = 15f;
            else if (KeyboardState.IsKeyReleased(Keys.C))
                camera.FOV = 70f;

            base.OnUpdateFrame(e);
        }

        //  Runs when window first opens
        protected override void OnLoad()
        {
            Debug.CreateContext();

            Debug.Log($"Started {this.Title}");
            Debug.Log("Settings");
            Debug.Log($"    Framelimit {this.RenderFrequency}");
            Debug.Log($"    Resolution {ClientSize.X} x {ClientSize.Y} borderless");

            Debug.Log($"OpenGL v{GL.GetString(StringName.Version)}");
            Debug.Log($"    {GL.GetString(StringName.Vendor)}");
            Debug.Log($"    {GL.GetString(StringName.Renderer)}");

            guiController = new ImGuiController(ClientSize.X, ClientSize.Y);
            camera = new Camera();

            GL.ClearColor(0.2f, 0.3f, 0.3f, 1.0f);

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
            textureArray = Texture2DArray.CreateFromFolder("resources/textures/block/", "blocks", 16, 16);
            textureArray.SetMinFilter(TextureMinFilter.Nearest);
            textureArray.SetMagFilter(TextureMagFilter.Nearest);
            textureArray.SetWrap(TextureCoordinate.S, TextureWrapMode.ClampToEdge);
            textureArray.Use(TextureUnit.Texture0);

            shader.SetInt("texture0", 0);

            base.OnLoad();
        }

        protected override void OnUnload()
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

            base.OnUnload();
        }

        protected override void OnRenderFrame(FrameEventArgs e)
        {
            //  Keep delta time updated for access outside events
            DeltaTime = (float)e.Time;

            //  Clear the buffer, enable depth testing, enable backface culling
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.CullFace);

            shader.Use();
            textureArray.Use(TextureUnit.Texture0);

            //  Translate
            degrees += (float)(16 * e.Time);
            Matrix4 transform =
                Matrix4.Identity
                * Matrix4.CreateRotationX(MathHelper.DegreesToRadians(degrees))
                * Matrix4.CreateRotationY(MathHelper.DegreesToRadians(degrees));
            transform *= Matrix4.CreateTranslation(0f, 0f, -3f);

            camera.Update();
            projection = Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(camera.FOV), (float)Size.X / (float)Size.Y, 0.1f, 100.0f);

            shader.SetMatrix4("transform", transform);
            shader.SetMatrix4("view", camera.view);
            shader.SetMatrix4("projection", projection);

            //  Draw triangles
            GL.BindVertexArray(VertexArrayObject);
            GL.DrawElements(PrimitiveType.Triangles, indices.Length, DrawElementsType.UnsignedInt, 0);

            //  Draw GUI elements
            //  Disable depth testing for this pass
            GL.Disable(EnableCap.DepthTest);

            guiController.Update(this, (float)e.Time);
            ImGui.ShowDemoWindow();
            guiController.Render();

            //  Grab GL errors
            Debug.TryLogGLError("OpenGL");

            //  End render, swap buffers
            Context.SwapBuffers();
            base.OnRenderFrame(e);
        }

        protected override void OnResize(ResizeEventArgs e)
        {
            GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);
            guiController.WindowResized(ClientSize.X, ClientSize.Y);
            base.OnResize(e);
        }

        protected override void OnTextInput(TextInputEventArgs e)
        {
            base.OnTextInput(e);

            guiController.PressChar((char)e.Unicode);
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);

            guiController.MouseScroll(e.Offset);
        }
    }
}

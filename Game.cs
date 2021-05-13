using System;
using ImGuiNET;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Swordfish;
using Swordfish.Rendering;

namespace waywardbeyond
{
    public class Game : GameWindow
    {
        private ImGuiController guiController;

        private Shader shader;
        private float[] vertices = {
            -0.5f, -0.5f, 0.0f, //Bottom-left vertex
            0.5f, -0.5f, 0.0f, //Bottom-right vertex
            0.0f,  0.5f, 0.0f  //Top vertex
        };

        int VertexBufferObject;
        int VertexArrayObject;

        public Game(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings) : base(gameWindowSettings, nativeWindowSettings)
        {
        }

        // This function runs on every update frame.
        protected override void OnUpdateFrame(FrameEventArgs e)
        {
            // Check if the Escape button is currently being pressed.
            if (KeyboardState.IsKeyDown(Keys.Escape))
            {
                // If it is, close the window.
                Close();
            }

            base.OnUpdateFrame(e);
        }

        //  Runs when window first opens
        protected override void OnLoad()
        {
            Debug.Log($"OpenGL v{GL.GetString(StringName.Version)}");
            Debug.Log($"    {GL.GetString(StringName.Vendor)}");
            Debug.Log($"    {GL.GetString(StringName.Renderer)}");

            guiController = new ImGuiController(ClientSize.X, ClientSize.Y);

            GL.ClearColor(0.2f, 0.3f, 0.3f, 1.0f);

            //  Setup buffer
            VertexBufferObject = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, VertexBufferObject);

            //  Send vertices to buffer
            //  Dynamic draw for data that like to change a lot
            //  Stream for constant
            //  Static for rare/never
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);

            //  Setup VAO
            VertexArrayObject = GL.GenVertexArray();
            GL.BindVertexArray(VertexArrayObject);

            //  Shaders
            shader = new Shader("shaders/test.vert", "shaders/test.frag", "Test");
            shader.Use();

            //  Tell openGL how to interpret vertex data
            GL.VertexAttribPointer(shader.GetAttribLocation("aPosition"), 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
            GL.EnableVertexAttribArray(shader.GetAttribLocation("aPosition"));

            base.OnLoad();
        }

        protected override void OnUnload()
        {
            //  Unbind resources
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindVertexArray(0);
            GL.UseProgram(0);

            // Delete resources
            GL.DeleteBuffer(VertexBufferObject);
            GL.DeleteVertexArray(VertexArrayObject);
            GL.DeleteProgram(shader.Handle);

            //  Dispose shaders
            shader.Dispose();

            base.OnUnload();
        }

        protected override void OnRenderFrame(FrameEventArgs e)
        {
            GL.Clear(ClearBufferMask.ColorBufferBit);

            //  Use the shaders and bind the VAO
            shader.Use();
            GL.BindVertexArray(VertexArrayObject);

            //  Draw the vertices
            GL.DrawArrays(PrimitiveType.Triangles, 0, 3);

            //  GUI
            guiController.Update(this, (float)e.Time);
            ImGui.ShowDemoWindow();
            guiController.Render();

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

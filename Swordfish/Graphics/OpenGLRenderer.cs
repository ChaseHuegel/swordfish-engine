using System.Drawing;
using Ninject;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;
using Swordfish.Library.Diagnostics;

namespace Swordfish.Graphics;

public class OpenGLRenderer : IRenderContext
{
    private IWindow? Window { get; set; }

    private GL Gl => gl ??= GL.GetApi(Window);
    private GL? gl;

    public void Initialize(IWindow window)
    {
        Window = window;
        Window.Closing += Cleanup;
        Window.FramebufferResize += Resize;
        Window.Render += Render;

        Debugger.Log("Renderer initialized.");
    }

    private void Cleanup()
    {
        Gl.Dispose();
    }

    private void Resize(Vector2D<int> size)
    {
        Gl.Viewport(size);
    }

    private void Render(double delta)
    {
        Gl.ClearColor(Color.FromArgb(255, (int)(0.08f * 255), (int)(0.1f * 255), (int)(0.14f * 255)));
        Gl.Clear((uint)ClearBufferMask.ColorBufferBit);
    }
}

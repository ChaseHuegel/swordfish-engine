using System.Collections.Concurrent;
using System.Numerics;
using Swordfish.Graphics.SilkNET;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.Extensions;

namespace Swordfish.Graphics;

internal class RenderContext : IRenderContext
{
    private readonly ConcurrentBag<IRenderTarget> RenderTargets = new();

    private readonly Camera Camera;

    private readonly IWindowContext WindowContext;

    public RenderContext(IWindowContext windowContext)
    {
        WindowContext = windowContext;

        Camera = new Camera(90, WindowContext.GetSize().GetRatio(), 0.001f, 1000f);
        WindowContext.Resized += OnWindowResized;
    }

    public void Initialize()
    {
        Debugger.Log("Renderer initialized.");
    }

    public void Render(double delta)
    {
        foreach (IRenderTarget target in RenderTargets)
        {
            target.Render(Camera);
        }
    }

    public void Bind(Shader shader)
    {
    }

    public void Bind(Texture texture)
    {
    }

    public void Bind(Mesh mesh)
    {
    }


    public void Bind(MeshRenderer meshRenderer)
    {
        //  TODO
        //  Get or create ShaderProgram, assign to Shader.Handle
        //  Get or create TexImage2D, assign to Texture.Handle
        //  Get or create VertexArrayObject, assign to Mesh.Handle
        //  Get or create GLRenderTarget, assign to MeshRenderer.Handle
        //  Store GLRenderTarget for rendering
        //  Set MeshRenderer.RenderContext
    }

    private void OnWindowResized(Vector2 newSize)
    {
        Camera.AspectRatio = newSize.GetRatio();
    }
}
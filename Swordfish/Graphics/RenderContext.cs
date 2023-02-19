using System.Collections.Concurrent;
using System.Numerics;
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

    public void Bind(IRenderTarget renderTarget)
    {
        RenderTargets.Add(renderTarget);
    }

    private void OnWindowResized(Vector2 newSize)
    {
        Camera.AspectRatio = newSize.GetRatio();
    }
}
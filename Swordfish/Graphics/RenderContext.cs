using System.Collections.Concurrent;
using Silk.NET.OpenGL;
using Swordfish.Graphics.SilkNET;
using Swordfish.Library.Diagnostics;

namespace Swordfish.Graphics;

public class RenderContext : IRenderContext
{
    private readonly ConcurrentBag<RenderTarget> RenderTargets = new();

    private readonly Camera Camera;

    public RenderContext()
    {
        Camera = new Camera(90);
    }

    public void Initialize()
    {
        Debugger.Log("Renderer initialized.");
    }

    public void Render(double delta)
    {
        foreach (RenderTarget target in RenderTargets)
        {
            target.Render(Camera);
        }
    }

    public void Bind(RenderTarget renderTarget)
    {
        RenderTargets.Add(renderTarget);
    }
}
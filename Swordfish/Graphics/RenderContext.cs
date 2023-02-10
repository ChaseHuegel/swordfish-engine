using System.Collections.Concurrent;
using Ninject;
using Silk.NET.OpenGL;
using Swordfish.Graphics.SilkNET;
using Swordfish.Library.Diagnostics;

namespace Swordfish.Graphics;

public class RenderContext : IRenderContext
{
    private GL GL => gl ??= SwordfishEngine.Kernel.Get<GL>();
    private GL gl;

    private ConcurrentBag<RenderTarget> RenderTargets = new();

    public void Initialize()
    {
        Debugger.Log("Renderer initialized.");
    }

    public void Render(double delta)
    {
        foreach (RenderTarget target in RenderTargets)
        {
            target.Render();
        }
    }

    public void Bind(RenderTarget renderTarget)
    {
        RenderTargets.Add(renderTarget);
    }
}
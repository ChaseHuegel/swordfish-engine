using Swordfish.Library.Diagnostics;

namespace Swordfish.Graphics;

public class RenderContext : IRenderContext
{
    public void Initialize()
    {
        Debugger.Log("Renderer initialized.");
    }
}
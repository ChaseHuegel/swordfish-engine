using System.Drawing;
using Swordfish.Library.Types;
using Swordfish.Types;

namespace Swordfish.Graphics;

public sealed class RectRenderer : IHandle
{
    public event EventHandler<EventArgs>? Disposed;

    public Rect2 Rect { get; }
    public Color Color { get; }
    public Material[] Materials { get; }

    public RectRenderer(Rect2 rect, params Material[] materials)
    {
        Rect = rect;
        Color = Color.White;
        Materials = materials;
    }
    
    public RectRenderer(Rect2 rect, Color color, params Material[] materials)
    {
        Rect = rect;
        Color = color;
        Materials = materials;
    }

    public void Dispose()
    {
        Disposed?.Invoke(this, EventArgs.Empty);
    }
}

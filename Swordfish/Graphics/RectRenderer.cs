using System.Drawing;
using Swordfish.Library.Types;
using Swordfish.Types;

namespace Swordfish.Graphics;

public sealed class RectRenderer : IHandle
{
    public event EventHandler<EventArgs>? Disposed;

    /// <inheritdoc/>
    public bool IsDisposed { get; private set; }

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
        if (IsDisposed)
        {
            return;
        }

        IsDisposed = true;
        Disposed?.Invoke(this, EventArgs.Empty);
        GC.SuppressFinalize(this);
    }
}

namespace Swordfish.Graphics;

public interface IHandle : IDisposable
{
    event EventHandler<EventArgs>? Disposed;
}

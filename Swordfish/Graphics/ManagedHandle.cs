using Swordfish.Library.Diagnostics;

namespace Swordfish.Graphics;

public abstract class Handle : IHandle
{
    public event EventHandler<EventArgs>? Disposed;

    protected volatile bool disposed;

    public void Dispose()
    {
        if (disposed)
        {
            Debugger.Log($"Attempted to dispose a {GetType()} that was already disposed.", LogType.WARNING);
            return;
        }

        disposed = true;
        Disposed?.Invoke(this, EventArgs.Empty);
        OnDisposed();
        GC.SuppressFinalize(this);
    }

    protected abstract void OnDisposed();
}

public abstract class ManagedHandle<TType> : Handle
{
    public TType Handle => handle ??= CreateHandle();
    private TType? handle;

    protected abstract TType CreateHandle();
}

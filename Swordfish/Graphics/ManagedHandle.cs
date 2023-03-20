using Swordfish.Library.Diagnostics;

namespace Swordfish.Graphics;

public abstract class Handle : IHandle
{
    public event EventHandler<EventArgs>? Disposed;

    protected volatile bool IsDisposed;

    public void Dispose()
    {
        if (IsDisposed)
        {
            Debugger.Log($"Attempted to dispose a {GetType()} that was already disposed.", LogType.WARNING);
            return;
        }

        IsDisposed = true;
        Disposed?.Invoke(this, EventArgs.Empty);
        OnDisposed();
        GC.SuppressFinalize(this);
    }

    protected abstract void OnDisposed();
}

public abstract class ManagedHandle<TType> : Handle
{
    public TType Handle
    {
        get
        {
            if (!handleCreated)
            {
                handle = CreateHandle();
                handleCreated = true;
            }

            return handle!;
        }
    }

    private TType? handle;
    private bool handleCreated;

    protected abstract TType CreateHandle();
}

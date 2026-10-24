using System;

namespace Swordfish.Library.Types;

public abstract class Handle : IHandle
{
    public event EventHandler<EventArgs> Disposed;

    private volatile bool _isDisposed;

    /// <inheritdoc/>
    public bool IsDisposed => _isDisposed;

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        Disposed?.Invoke(this, EventArgs.Empty);
        OnDisposed();
        GC.SuppressFinalize(this);
    }

    protected abstract void OnDisposed();
}
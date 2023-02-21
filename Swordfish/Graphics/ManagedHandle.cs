using Swordfish.Library.Diagnostics;

namespace Swordfish.Graphics.SilkNET.OpenGL;

public abstract class ManagedHandle<TType> : IHandle
{
    public TType Handle => handle ??= CreateHandle();

    protected volatile bool Disposed;

    private TType? handle;

    public void Dispose()
    {
        if (Disposed)
        {
            Debugger.Log($"Attempted to dispose a {GetType()} that was already disposed.", LogType.WARNING);
            return;
        }

        Disposed = true;
        OnDisposed();
        GC.SuppressFinalize(this);
    }

    protected abstract TType CreateHandle();

    protected abstract void OnDisposed();
}

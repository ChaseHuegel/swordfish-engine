using Swordfish.Library.Types;

namespace Swordfish.Graphics.SilkNET.OpenGL;

public abstract class GLHandle : ManagedHandle<uint>, IBindable
{
    public readonly struct Scope : IDisposable
    {
        private readonly GLHandle _handle;

        public Scope(in GLHandle handle)
        {
            _handle = handle;
            handle.Bind();
        }

        public void Dispose()
        {
            _handle.Unbind();
        }
    }
    
    public void Bind()
    {
        if (IsDisposed)
        {
            return;
        }

        BindHandle();
    }

    public void Unbind()
    {
        if (IsDisposed)
        {
            return;
        }

        UnbindHandle();
    }

    public Scope Use()
    {
        return new Scope(this);
    }

    protected abstract void BindHandle();
    protected abstract void UnbindHandle();
}
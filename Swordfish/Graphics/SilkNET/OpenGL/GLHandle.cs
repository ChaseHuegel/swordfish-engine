using Swordfish.Library.Types;

namespace Swordfish.Graphics.SilkNET.OpenGL;

internal abstract class GLHandle : ManagedHandle<uint>, IBindable
{
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
    
    public readonly struct Scope : IDisposable
    {
        private readonly GLHandle _handle;

        public Scope(in GLHandle handle)
        {
            handle.Bind();
            _handle = handle;
        }

        public void Dispose()
        {
            _handle.Unbind();
        }
    }
}
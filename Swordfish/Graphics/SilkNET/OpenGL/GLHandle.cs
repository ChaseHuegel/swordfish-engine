using System.Diagnostics;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.Types;
using Debugger = Swordfish.Library.Diagnostics.Debugger;

namespace Swordfish.Graphics.SilkNET.OpenGL;

public abstract class GLHandle : ManagedHandle<uint>, IBindable
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

    protected abstract void BindHandle();
    protected abstract void UnbindHandle();
}
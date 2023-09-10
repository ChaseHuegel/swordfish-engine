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
            Debugger.Log($"Attempted to bind {this} but it is disposed.", LogType.ERROR);
            return;
        }

        BindHandle();
    }

    public void Unbind()
    {
        if (IsDisposed)
        {
            Debugger.Log($"Attempted to unbund {this} but it is disposed.", LogType.ERROR);
            return;
        }

        UnbindHandle();
    }

    protected abstract void BindHandle();
    protected abstract void UnbindHandle();
}
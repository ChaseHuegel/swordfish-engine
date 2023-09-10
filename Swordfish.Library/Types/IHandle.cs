using System;

namespace Swordfish.Library.Types
{
    public interface IHandle : IDisposable
    {
        event EventHandler<EventArgs> Disposed;
    }
}

using System;
using Swordfish.Library.Diagnostics;

namespace Swordfish.Library.Types
{
    public abstract class Handle : IHandle
    {
        public event EventHandler<EventArgs> Disposed;

        protected volatile bool IsDisposed;

        public void Dispose()
        {
            if (IsDisposed)
            {
                return;
            }

            IsDisposed = true;
            Disposed?.Invoke(this, EventArgs.Empty);
            OnDisposed();
            GC.SuppressFinalize(this);
        }

        protected abstract void OnDisposed();
    }
}
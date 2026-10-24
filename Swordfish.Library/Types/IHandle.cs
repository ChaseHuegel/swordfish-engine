using System;

namespace Swordfish.Library.Types;

public interface IHandle : IDisposable
{
    event EventHandler<EventArgs> Disposed;

    /// <summary>
    /// Gets whether the handle has been disposed.
    /// </summary>
    bool IsDisposed { get; }
}
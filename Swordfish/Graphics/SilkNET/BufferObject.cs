using Silk.NET.OpenGL;
using Swordfish.Library.Diagnostics;

namespace Swordfish.Graphics.SilkNET;

public sealed class BufferObject<TData> : IDisposable
    where TData : unmanaged
{
    public readonly int Length;

    private GL GL => gl ??= SwordfishEngine.Kernel.Get<GL>();
    private GL gl;

    private readonly uint Handle;
    private readonly BufferTargetARB BufferType;

    private volatile bool Disposed;

    public unsafe BufferObject(Span<TData> data, BufferTargetARB bufferType)
    {
        Length = data.Length;
        BufferType = bufferType;
        Handle = GL!.GenBuffer();

        Bind();
        fixed (void* dataPtr = data)
        {
            GL.BufferData(BufferType, (nuint)(data.Length * sizeof(TData)), dataPtr, BufferUsageARB.StaticDraw);
        }
    }

    public void Dispose()
    {
        if (Disposed)
        {
            Debugger.Log($"Attempted to dispose {this} but it is already disposed.", LogType.WARNING);
            return;
        }

        Disposed = true;
        GL.DeleteBuffer(Handle);
    }

    public void Bind()
    {
        if (Disposed)
        {
            Debugger.Log($"Attempted to bind {this} but it is disposed.", LogType.ERROR);
            return;
        }

        GL.BindBuffer(BufferType, Handle);
    }
}
using Silk.NET.OpenGL;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.Types;
using Swordfish.Util;

namespace Swordfish.Graphics.SilkNET.OpenGL;

internal sealed class BufferObject<TData> : Handle
    where TData : unmanaged
{
    private readonly GL GL;
    public readonly int Length;
    private readonly uint Handle;
    private readonly BufferTargetARB BufferType;

    public unsafe BufferObject(GL gl, Span<TData> data, BufferTargetARB bufferType, BufferUsageARB usage = BufferUsageARB.StaticDraw)
    {
        GL = gl;
        Length = data.Length;
        BufferType = bufferType;
        Handle = GL!.GenBuffer();

        Bind();
        fixed (void* dataPtr = data)
        {
            nuint bufferSize = new((uint)(data.Length * sizeof(TData)));
            GL.BufferData(BufferType, bufferSize, dataPtr, usage);
        }
    }

    protected override void OnDisposed()
    {
        GL.DeleteBuffer(Handle);
    }

    public void Bind()
    {
        if (IsDisposed)
        {
            Debugger.Log($"Attempted to bind {this} but it is disposed.", LogType.ERROR);
            return;
        }

        GL.BindBuffer(BufferType, Handle);
    }
}

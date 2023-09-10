using Silk.NET.OpenGL;
using Swordfish.Library.Diagnostics;

namespace Swordfish.Graphics.SilkNET.OpenGL;

internal sealed class BufferObject<TData> : GLHandle
    where TData : unmanaged
{
    private readonly GL GL;
    public readonly int Length;
    private readonly BufferTargetARB BufferType;

    public unsafe BufferObject(GL gl, Span<TData> data, BufferTargetARB bufferType, BufferUsageARB usage = BufferUsageARB.StaticDraw)
    {
        GL = gl;
        Length = data.Length;
        BufferType = bufferType;

        Bind();
        fixed (void* dataPtr = data)
        {
            nuint bufferSize = new((uint)(data.Length * sizeof(TData)));
            GL.BufferData(BufferType, bufferSize, dataPtr, usage);
        }
    }

    protected override uint CreateHandle()
    {
        return GL.GenBuffer();
    }

    protected override void FreeHandle()
    {
        GL.DeleteBuffer(Handle);
    }

    protected override void BindHandle()
    {
        GL.BindBuffer(BufferType, Handle);
    }

    protected override void UnbindHandle()
    {
        GL.BindBuffer(BufferType, 0);
    }
}

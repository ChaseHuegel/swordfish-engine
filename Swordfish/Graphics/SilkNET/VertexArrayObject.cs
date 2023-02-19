using Silk.NET.OpenGL;
using Swordfish.Library.Diagnostics;

namespace Swordfish.Graphics.SilkNET;

internal sealed class VertexArrayObject<TVertexType, TElementType> : IDisposable
    where TVertexType : unmanaged
    where TElementType : unmanaged
{
    private readonly GL GL;

    private readonly uint Handle;

    private volatile bool Disposed;

    public VertexArrayObject(GL gl, BufferObject<TVertexType> vertexBufferObject, BufferObject<TElementType> elementBufferObject)
    {
        GL = gl;
        Handle = GL.GenVertexArray();

        Bind();
        vertexBufferObject.Bind();
        elementBufferObject.Bind();
    }

    public void Dispose()
    {
        if (Disposed)
        {
            Debugger.Log($"Attempted to dispose {this} but it is already disposed.", LogType.WARNING);
            return;
        }

        Disposed = true;
        GL.DeleteVertexArray(Handle);
    }

    public void Bind()
    {
        if (Disposed)
        {
            Debugger.Log($"Attempted to bind {this} but it is disposed.", LogType.ERROR);
            return;
        }

        GL.BindVertexArray(Handle);
    }

    public unsafe void SetVertexAttributePointer(uint index, int count, VertexAttribPointerType type, uint vertexSize, int offset)
    {
        GL.VertexAttribPointer(index, count, type, false, vertexSize * (uint)sizeof(TVertexType), (void*)(offset * sizeof(TVertexType)));
        GL.EnableVertexAttribArray(index);
    }
}

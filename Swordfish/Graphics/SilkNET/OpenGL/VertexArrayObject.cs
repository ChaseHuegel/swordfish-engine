using Silk.NET.OpenGL;
using Swordfish.Library.Diagnostics;

namespace Swordfish.Graphics.SilkNET.OpenGL;

internal sealed class VertexArrayObject32 : VertexArrayObject<float, uint>
{
    public VertexArrayObject32(GL gl, BufferObject<float> vertexBufferObject, BufferObject<uint> elementBufferObject)
        : base(gl, vertexBufferObject, elementBufferObject) { }
}

internal class VertexArrayObject<TVertexType, TElementType> : ManagedHandle<uint>
    where TVertexType : unmanaged
    where TElementType : unmanaged
{
    private readonly GL GL;

    public VertexArrayObject(GL gl, BufferObject<TVertexType> vertexBufferObject, BufferObject<TElementType> elementBufferObject)
    {
        GL = gl;

        Bind();
        vertexBufferObject.Bind();
        elementBufferObject.Bind();
    }

    protected override uint CreateHandle()
    {
        return GL.GenVertexArray();
    }

    protected override void OnDisposed()
    {
        GL.DeleteVertexArray(Handle);
    }

    public void Bind()
    {
        if (disposed)
        {
            Debugger.Log($"Attempted to bind {this} but it is disposed.", LogType.ERROR);
            return;
        }

        GL.BindVertexArray(Handle);
    }

    public unsafe void SetVertexAttributePointer(uint index, int count, VertexAttribPointerType type, uint stride, int offset)
    {
        GL.EnableVertexAttribArray(index);
        GL.VertexAttribPointer(index, count, type, false, stride, (void*)offset);
    }

    public unsafe void SetVertexAttribute(uint index, int count, VertexAttribPointerType type, uint vertexSize, int offset)
    {
        GL.EnableVertexAttribArray(index);
        GL.VertexAttribPointer(index, count, type, false, vertexSize * (uint)sizeof(TVertexType), (void*)(offset * sizeof(TVertexType)));
    }

    public void SetVertexAttributeDivisor(uint index, uint divisor)
    {
        GL.VertexAttribDivisor(index, divisor);
    }
}

using System.Runtime.CompilerServices;
using Silk.NET.OpenGL;
using Swordfish.Library.Diagnostics;

namespace Swordfish.Graphics.SilkNET.OpenGL;

internal sealed class VertexArrayObject32 : VertexArrayObject<float, uint>
{
    public VertexArrayObject32(GL gl, BufferObject<float> vertexBufferObject, BufferObject<uint> elementBufferObject)
        : base(gl, vertexBufferObject, elementBufferObject) { }
}

internal class VertexArrayObject<TVertexType, TElementType> : GLHandle, IEquatable<VertexArrayObject<TVertexType, TElementType>>
    where TVertexType : unmanaged
    where TElementType : unmanaged
{
    internal BufferObject<TVertexType> VertexBufferObject { get; }
    internal BufferObject<TElementType> ElementBufferObject { get; }

    private readonly GL GL;

    public VertexArrayObject(GL gl, BufferObject<TVertexType> vertexBufferObject, BufferObject<TElementType> elementBufferObject)
    {
        GL = gl;
        VertexBufferObject = vertexBufferObject;
        ElementBufferObject = elementBufferObject;

        Bind();
        VertexBufferObject.Bind();
        ElementBufferObject.Bind();
    }

    protected override uint CreateHandle()
    {
        return GL.GenVertexArray();
    }

    protected override void FreeHandle()
    {
        GL.DeleteVertexArray(Handle);
    }

    protected override void BindHandle()
    {
        GL.BindVertexArray(Handle);
    }

    protected override void UnbindHandle()
    {
        GL.BindVertexArray(0);
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(VertexArrayObject<TVertexType, TElementType>? other)
    {
        return Handle.Equals(other?.Handle);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(this, obj))
            return true;

        if (obj is not VertexArrayObject<TVertexType, TElementType> other)
            return false;

        return Equals(other);
    }

    public override int GetHashCode()
    {
        return (int)Handle;
    }

    public override string? ToString()
    {
        return base.ToString() + $"[{Handle}]";
    }
}

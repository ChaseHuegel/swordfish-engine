using System.Runtime.CompilerServices;
using Silk.NET.OpenGL;
// ReSharper disable UnusedMember.Global

namespace Swordfish.Graphics.SilkNET.OpenGL;

internal sealed class VertexArrayObject32(GL gl, BufferObject<float> vertexBufferObject, BufferObject<uint> elementBufferObject)
    : VertexArrayObject<float, uint>(gl, vertexBufferObject, elementBufferObject);

internal class VertexArrayObject<TVertexType> : GLHandle, IEquatable<VertexArrayObject<TVertexType>>
    where TVertexType : unmanaged
{
    internal BufferObject<TVertexType> VertexBufferObject { get; }

    private readonly GL _gl;

    public VertexArrayObject(GL gl, BufferObject<TVertexType> vertexBufferObject)
    {
        _gl = gl;
        VertexBufferObject = vertexBufferObject;

        Bind();
        VertexBufferObject.Bind();
    }

    protected override uint CreateHandle()
    {
        return _gl.GenVertexArray();
    }

    protected override void FreeHandle()
    {
        _gl.DeleteVertexArray(Handle);
    }

    protected override void BindHandle()
    {
        _gl.BindVertexArray(Handle);
    }

    protected override void UnbindHandle()
    {
        _gl.BindVertexArray(0);
    }

    public unsafe void SetVertexAttributePointer(uint index, int count, VertexAttribPointerType type, uint stride, int offset)
    {
        _gl.EnableVertexAttribArray(index);
        _gl.VertexAttribPointer(index, count, type, false, stride, (void*)offset);
    }

    public unsafe void SetVertexAttribute(uint index, int count, VertexAttribPointerType type, uint vertexSize, int offset)
    {
        _gl.EnableVertexAttribArray(index);
        _gl.VertexAttribPointer(index, count, type, false, vertexSize * (uint)sizeof(TVertexType), (void*)(offset * sizeof(TVertexType)));
    }

    public void SetVertexAttributeDivisor(uint index, uint divisor)
    {
        _gl.VertexAttribDivisor(index, divisor);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(VertexArrayObject<TVertexType>? other)
    {
        return Handle.Equals(other?.Handle);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj is not VertexArrayObject<TVertexType> other)
        {
            return false;
        }

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

internal class VertexArrayObject<TVertexType, TElementType> : GLHandle, IEquatable<VertexArrayObject<TVertexType, TElementType>>
    where TVertexType : unmanaged
    where TElementType : unmanaged
{
    internal BufferObject<TVertexType> VertexBufferObject { get; }
    internal BufferObject<TElementType> ElementBufferObject { get; }

    private readonly GL _gl;

    public VertexArrayObject(GL gl, BufferObject<TVertexType> vertexBufferObject, BufferObject<TElementType> elementBufferObject)
    {
        _gl = gl;
        VertexBufferObject = vertexBufferObject;
        ElementBufferObject = elementBufferObject;

        Bind();
        VertexBufferObject.Bind();
        ElementBufferObject.Bind();
    }

    protected override uint CreateHandle()
    {
        return _gl.GenVertexArray();
    }

    protected override void FreeHandle()
    {
        _gl.DeleteVertexArray(Handle);
    }

    protected override void BindHandle()
    {
        _gl.BindVertexArray(Handle);
    }

    protected override void UnbindHandle()
    {
        _gl.BindVertexArray(0);
    }

    public unsafe void SetVertexAttributePointer(uint index, int count, VertexAttribPointerType type, uint stride, int offset)
    {
        _gl.EnableVertexAttribArray(index);
        _gl.VertexAttribPointer(index, count, type, false, stride, (void*)offset);
    }

    public unsafe void SetVertexAttribute(uint index, int count, VertexAttribPointerType type, uint vertexSize, int offset)
    {
        _gl.EnableVertexAttribArray(index);
        _gl.VertexAttribPointer(index, count, type, false, vertexSize * (uint)sizeof(TVertexType), (void*)(offset * sizeof(TVertexType)));
    }

    public void SetVertexAttributeDivisor(uint index, uint divisor)
    {
        _gl.VertexAttribDivisor(index, divisor);
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
        {
            return true;
        }

        return obj is VertexArrayObject<TVertexType, TElementType> other && Equals(other);
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

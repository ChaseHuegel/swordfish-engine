using Silk.NET.OpenGL;

namespace Swordfish.Graphics.SilkNET.OpenGL;

internal sealed class BufferObject<TData> : GLHandle
    where TData : unmanaged
{
    private readonly GL _gl;
    public readonly int Length;
    private readonly BufferTargetARB _bufferType;
    private readonly BufferUsageARB _usage;

    public unsafe BufferObject(GL gl, int size, BufferTargetARB bufferType, BufferUsageARB usage = BufferUsageARB.StaticDraw, uint? index = null)
     : this(gl, new Span<TData>(pointer: null, size), bufferType, usage, index) { }
    
    public unsafe BufferObject(GL gl, Span<TData> data, BufferTargetARB bufferType, BufferUsageARB usage = BufferUsageARB.StaticDraw, uint? index = null)
    {
        _gl = gl;
        Length = data.Length;
        _bufferType = bufferType;
        _usage = usage;

        using Scope _ = Use();
        fixed (void* dataPtr = data)
        {
            nuint bufferSize = new((uint)(data.Length * sizeof(TData)));
            _gl.BufferData(_bufferType, bufferSize, dataPtr, _usage);
        }

        if (index != null)
        {
            gl.BindBufferBase(bufferType, index.Value, Handle);
        }
    }

    public unsafe void UpdateData(Span<TData> data)
    {
        Bind();
        fixed (void* dataPtr = data)
        {
            nuint bufferSize = new((uint)(data.Length * sizeof(TData)));
            _gl.BufferData(_bufferType, bufferSize, dataPtr, _usage);
        }
        Unbind();
    }
    
    public unsafe void Resize(int size)
    {
        Bind();
        nuint bufferSize = new((uint)(size * sizeof(TData)));
        _gl.BufferData(_bufferType, bufferSize, data: null, _usage);
        Unbind();
    }

    protected override uint CreateHandle()
    {
        return _gl.GenBuffer();
    }

    protected override void FreeHandle()
    {
        _gl.DeleteBuffer(Handle);
    }

    protected override void BindHandle()
    {
        _gl.BindBuffer(_bufferType, Handle);
    }

    protected override void UnbindHandle()
    {
        _gl.BindBuffer(_bufferType, 0);
    }
}

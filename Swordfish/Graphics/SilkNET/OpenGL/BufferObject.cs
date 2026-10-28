using Silk.NET.OpenGL;

namespace Swordfish.Graphics.SilkNET.OpenGL;

internal sealed class BufferObject<TData> : GLHandle
    where TData : unmanaged
{
    private readonly GL _gl;
    public readonly int Length;
    public int ByteCapacity { get; private set; }
    private readonly BufferTargetARB _bufferType;
    private readonly BufferUsageARB _usage;

    public unsafe BufferObject(GL gl, int size, BufferTargetARB bufferType, BufferUsageARB usage = BufferUsageARB.StaticDraw, uint? index = null)
     : this(gl, new Span<TData>(pointer: null, size), bufferType, usage, index) { }
    
    public unsafe BufferObject(GL gl, Span<TData> data, BufferTargetARB bufferType, BufferUsageARB usage = BufferUsageARB.StaticDraw, uint? index = null)
    {
        _gl = gl;
        Length = data.Length;
        ByteCapacity = data.Length * sizeof(TData);
        _bufferType = bufferType;
        _usage = usage;

        using Scope _ = Use();
        fixed (void* dataPtr = data)
        {
            nuint bufferSize = new((uint)ByteCapacity);
            _gl.BufferData(_bufferType, bufferSize, dataPtr, _usage);
        }

        if (index != null)
        {
            gl.BindBufferBase(bufferType, index.Value, Handle);
        }
    }

    public unsafe void UpdateData(ReadOnlySpan<TData> data)
    {
        Bind();
        fixed (void* dataPtr = data)
        {
            int requiredBytes = data.Length * sizeof(TData);
            if (requiredBytes > ByteCapacity)
            {
                ByteCapacity = requiredBytes;
                _gl.BufferData(_bufferType, (nuint)requiredBytes, dataPtr, _usage);
            }
            else
            {
                _gl.BufferSubData(_bufferType, 0, (nuint)requiredBytes, dataPtr);
            }
        }
        Unbind();
    }

    public unsafe void UpdateData(Span<TData> data) => UpdateData((ReadOnlySpan<TData>)data);
    
    public unsafe void Resize(int size)
    {
        Bind();
        ByteCapacity = size * sizeof(TData);
        _gl.BufferData(_bufferType, (nuint)ByteCapacity, data: null, _usage);
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

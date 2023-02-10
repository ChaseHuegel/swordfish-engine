using Ninject;
using Silk.NET.OpenGL;
using Swordfish.Library.Diagnostics;

namespace Swordfish.Graphics.SilkNET;

public sealed class RenderTarget : IDisposable
{
    private GL GL => gl ??= SwordfishEngine.Kernel.Get<GL>();
    private GL gl;

    private readonly BufferObject<float> VertexBufferObject;
    private readonly BufferObject<uint> ElementBufferObject;
    private readonly VertexArrayObject<float, uint> VertexArrayObject;

    private Shader Shader;

    private volatile bool Disposed;

    public RenderTarget(Span<float> vertices, Span<uint> indices, Shader shader)
    {
        Shader = shader;

        VertexBufferObject = new BufferObject<float>(vertices, BufferTargetARB.ArrayBuffer);
        ElementBufferObject = new BufferObject<uint>(indices, BufferTargetARB.ElementArrayBuffer);
        VertexArrayObject = new VertexArrayObject<float, uint>(VertexBufferObject, ElementBufferObject);

        VertexArrayObject.SetVertexAttributePointer(0, 3, VertexAttribPointerType.Float, 7, 0);
        VertexArrayObject.SetVertexAttributePointer(1, 4, VertexAttribPointerType.Float, 7, 3);
    }

    public void Dispose()
    {
        if (Disposed)
        {
            Debugger.Log($"Attempted to dispose {this} but it is already disposed.", LogType.WARNING);
            return;
        }

        Disposed = true;
        VertexBufferObject.Dispose();
        ElementBufferObject.Dispose();
        VertexArrayObject.Dispose();
        Shader.Dispose();
    }

    public unsafe void Render()
    {
        VertexArrayObject.Bind();
        Shader.Use();
        Shader.SetUniform("uBlue", (float)Math.Sin(DateTime.Now.Millisecond / 1000f * Math.PI));
        GL.DrawElements(PrimitiveType.Triangles, (uint)ElementBufferObject.Length, DrawElementsType.UnsignedInt, (void*)0);
    }
}

using Ninject;
using Silk.NET.OpenGL;
using Swordfish.Library.Diagnostics;
using Swordfish.Util;

namespace Swordfish.Graphics.SilkNET;

public sealed class RenderTarget : IDisposable
{
    private GL GL => gl ??= SwordfishEngine.Kernel.Get<GL>();
    private GL gl;

    private readonly BufferObject<float> VertexBufferObject;
    private readonly BufferObject<uint> ElementBufferObject;
    private readonly VertexArrayObject<float, uint> VertexArrayObject;

    private Shader Shader;
    private Texture Texture;

    private volatile bool Disposed;

    public RenderTarget(Span<float> vertices, Span<uint> indices, Shader shader, Texture texture)
    {
        Shader = shader;
        Texture = texture;

        VertexBufferObject = new BufferObject<float>(vertices, BufferTargetARB.ArrayBuffer);
        ElementBufferObject = new BufferObject<uint>(indices, BufferTargetARB.ElementArrayBuffer);
        VertexArrayObject = new VertexArrayObject<float, uint>(VertexBufferObject, ElementBufferObject);

        uint attributeLocation = Shader.GetAttributeLocation("in_position");
        VertexArrayObject.SetVertexAttributePointer(attributeLocation, 3, VertexAttribPointerType.Float, 10, 0);

        attributeLocation = Shader.GetAttributeLocation("in_color");
        VertexArrayObject.SetVertexAttributePointer(attributeLocation, 4, VertexAttribPointerType.Float, 10, 3);

        attributeLocation = Shader.GetAttributeLocation("in_uv");
        VertexArrayObject.SetVertexAttributePointer(attributeLocation, 3, VertexAttribPointerType.Float, 10, 7);
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
        Texture.Dispose();
    }

    public unsafe void Render()
    {
        VertexArrayObject.Bind();

        Shader.Use();

        Texture.Bind(TextureUnit.Texture0);
        Shader.SetUniform("texture0", 0);

        GL.DrawElements(PrimitiveType.Triangles, (uint)ElementBufferObject.Length, DrawElementsType.UnsignedInt, (void*)0);
    }
}

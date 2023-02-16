using System.Numerics;
using Ninject;
using Silk.NET.OpenGL;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.Types;
using Swordfish.Library.Util;

namespace Swordfish.Graphics.SilkNET;

public sealed class RenderTarget : IDisposable
{
    //  Reflects the Z axis.
    //  In openGL, positive Z is coming towards to viewer. We want it to extend away.
    private static readonly Matrix4x4 ReflectionMatrix = new(
        1, 0, 0, 0,
        0, 1, 0, 0,
        0, 0, -1, 0,
        0, 0, 0, 1
    );

    private GL GL => gl ??= SwordfishEngine.Kernel.Get<GL>();
    private GL gl;

    private IWindowContext Window => window ??= SwordfishEngine.Kernel.Get<IWindowContext>();
    private IWindowContext window;

    public Transform Transform { get; set; } = new();

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

    public unsafe void Render(Camera camera)
    {
        VertexArrayObject.Bind();

        Shader.Use();

        Texture.Bind(TextureUnit.Texture0);
        Shader.SetUniform("texture0", 0);

        var model = Transform.ToMatrix4x4() * ReflectionMatrix;
        var view = camera.Transform.ToMatrix4x4();
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(MathS.DegreesToRadians * camera.FOV, Window.Resolution.X / Window.Resolution.Y, 0.001f, 100f);

        Shader.SetUniform("model", model);
        Shader.SetUniform("view", view);
        Shader.SetUniform("projection", projection);

        GL.DrawElements(PrimitiveType.Triangles, (uint)ElementBufferObject.Length, DrawElementsType.UnsignedInt, (void*)0);
    }
}

using System.Numerics;
using Silk.NET.OpenGL;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.Types;

namespace Swordfish.Graphics.SilkNET.OpenGL;

internal sealed class GLRenderTarget : IDisposable
{
    //  Reflects the Z axis.
    //  In openGL, positive Z is coming towards to viewer. We want it to extend away.
    private static readonly Matrix4x4 ReflectionMatrix = new(
        1, 0, 0, 0,
        0, 1, 0, 0,
        0, 0, -1, 0,
        0, 0, 0, 1
    );

    private readonly GL GL;

    public Transform Transform { get; set; } = new();

    private BufferObject<float> VertexBufferObject;
    private BufferObject<uint> ElementBufferObject;
    private VertexArrayObject<float, uint> VertexArrayObject;

    private ShaderProgram Shader;
    private TexImage2D Texture;

    private volatile bool Disposed;

    public GLRenderTarget(GL gl, Span<float> vertices, Span<uint> indices, ShaderProgram shader, TexImage2D texture)
    {
        GL = gl;
        Shader = shader;
        Texture = texture;

        float[] verticesArray = vertices.ToArray();
        uint[] indiciesArray = indices.ToArray();

        VertexBufferObject = new BufferObject<float>(GL, verticesArray, BufferTargetARB.ArrayBuffer);
        ElementBufferObject = new BufferObject<uint>(GL, indiciesArray, BufferTargetARB.ElementArrayBuffer);
        VertexArrayObject = new VertexArrayObject<float, uint>(GL, VertexBufferObject, ElementBufferObject);

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
        var projection = camera.GetProjection();

        Shader.SetUniform("model", model);
        Shader.SetUniform("view", view);
        Shader.SetUniform("projection", projection);

        GL.DrawElements(PrimitiveType.Triangles, (uint)ElementBufferObject.Length, DrawElementsType.UnsignedInt, (void*)0);
    }
}

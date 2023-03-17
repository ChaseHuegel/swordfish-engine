using System.Numerics;
using Silk.NET.OpenGL;
using Swordfish.Library.Types;

namespace Swordfish.Graphics.SilkNET.OpenGL;

internal sealed class GLRenderTarget : Handle, IRenderTarget
{
    public const int VertexDataLength = 13 + 16;

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

    internal readonly BufferObject<Matrix4x4> ModelsArrayBufferObject;
    internal readonly BufferObject<float> VertexBufferObject;
    internal readonly BufferObject<uint> ElementBufferObject;
    internal readonly VertexArrayObject<float, uint> VertexArrayObject;

    internal readonly GLMaterial[] Materials;

    public unsafe GLRenderTarget(GL gl, Transform transform, Span<float> vertices, Span<uint> indices, params GLMaterial[] materials)
    {
        GL = gl;
        Transform = transform;
        Materials = materials;

        float[] verticesArray = vertices.ToArray();
        uint[] indiciesArray = indices.ToArray();
        VertexBufferObject = new BufferObject<float>(GL, verticesArray, BufferTargetARB.ArrayBuffer);
        ElementBufferObject = new BufferObject<uint>(GL, indiciesArray, BufferTargetARB.ElementArrayBuffer);
        VertexArrayObject = new VertexArrayObject<float, uint>(GL, VertexBufferObject, ElementBufferObject);

        VertexBufferObject.Bind();
        VertexArrayObject.SetVertexAttribute(0, 3, VertexAttribPointerType.Float, VertexDataLength, 0);
        VertexArrayObject.SetVertexAttribute(1, 4, VertexAttribPointerType.Float, VertexDataLength, 3);
        VertexArrayObject.SetVertexAttribute(2, 3, VertexAttribPointerType.Float, VertexDataLength, 7);
        VertexArrayObject.SetVertexAttribute(3, 3, VertexAttribPointerType.Float, VertexDataLength, 10);

        ModelsArrayBufferObject = new BufferObject<Matrix4x4>(GL, Array.Empty<Matrix4x4>(), BufferTargetARB.ArrayBuffer, BufferUsageARB.DynamicDraw);
        ModelsArrayBufferObject.Bind();
        for (uint i = 0; i < 4; i++)
        {
            VertexArrayObject.SetVertexAttributePointer(4 + i, 4, VertexAttribPointerType.Float, (uint)sizeof(Matrix4x4), (int)(i * sizeof(float) * 4));
            VertexArrayObject.SetVertexAttributeDivisor(4 + i, 1);
        }

        GL.BindVertexArray(0);

        for (int i = 0; i < Materials.Length; i++)
        {
            ShaderProgram shaderProgram = Materials[i].ShaderProgram;
            shaderProgram.BindAttributeLocation("in_position", 0);
            shaderProgram.BindAttributeLocation("in_color", 1);
            shaderProgram.BindAttributeLocation("in_uv", 2);
            shaderProgram.BindAttributeLocation("in_normal", 3);
            shaderProgram.BindAttributeLocation("model", 4);
        }
    }

    protected override void OnDisposed()
    {
        //  TODO need to not dispose (potentially) shared objects.
        VertexBufferObject.Dispose();
        ElementBufferObject.Dispose();
        VertexArrayObject.Dispose();
        for (int i = 0; i < Materials.Length; i++)
            Materials[i].Dispose();
    }

    public unsafe void Render(Camera camera)
    {
        var model = Transform.ToMatrix4x4() * ReflectionMatrix;
        var view = camera.Transform.ToMatrix4x4();
        var projection = camera.GetProjection();

        VertexArrayObject.Bind();

        for (int i = 0; i < Materials.Length; i++)
        {
            GLMaterial material = Materials[i];

            material.Use();
            material.ShaderProgram.SetUniform("model", model);
            material.ShaderProgram.SetUniform("view", view);
            material.ShaderProgram.SetUniform("projection", projection);
        }

        GL.DrawElements(PrimitiveType.Triangles, (uint)ElementBufferObject.Length, DrawElementsType.UnsignedInt, (void*)0);
    }
}

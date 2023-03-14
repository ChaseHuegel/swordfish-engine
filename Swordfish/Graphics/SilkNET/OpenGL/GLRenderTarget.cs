using System.Numerics;
using Silk.NET.OpenGL;
using Swordfish.Library.Types;

namespace Swordfish.Graphics.SilkNET.OpenGL;

internal sealed class GLRenderTarget : Handle, IRenderTarget
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

    private readonly BufferObject<float> VertexBufferObject;
    private readonly BufferObject<uint> ElementBufferObject;
    private readonly VertexArrayObject<float, uint> VertexArrayObject;

    private readonly GLMaterial[] Materials;

    public GLRenderTarget(GL gl, Span<float> vertices, Span<uint> indices, params GLMaterial[] materials)
    {
        GL = gl;
        Materials = materials;

        float[] verticesArray = vertices.ToArray();
        uint[] indiciesArray = indices.ToArray();
        VertexBufferObject = new BufferObject<float>(GL, verticesArray, BufferTargetARB.ArrayBuffer);
        ElementBufferObject = new BufferObject<uint>(GL, indiciesArray, BufferTargetARB.ElementArrayBuffer);
        VertexArrayObject = new VertexArrayObject<float, uint>(GL, VertexBufferObject, ElementBufferObject);

        for (int i = 0; i < Materials.Length; i++)
        {
            ShaderProgram shaderProgram = Materials[i].ShaderProgram;

            uint attributeLocation = shaderProgram.GetAttributeLocation("in_position");
            VertexArrayObject.SetVertexAttributePointer(attributeLocation, 3, VertexAttribPointerType.Float, 10, 0);

            attributeLocation = shaderProgram.GetAttributeLocation("in_color");
            VertexArrayObject.SetVertexAttributePointer(attributeLocation, 4, VertexAttribPointerType.Float, 10, 3);

            attributeLocation = shaderProgram.GetAttributeLocation("in_uv");
            VertexArrayObject.SetVertexAttributePointer(attributeLocation, 3, VertexAttribPointerType.Float, 10, 7);
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

using System.Numerics;
using System.Runtime.CompilerServices;
using Silk.NET.OpenGL;
using Swordfish.Library.Types;

namespace Swordfish.Graphics.SilkNET.OpenGL;

internal sealed class GLRenderTarget : Handle, IRenderTarget, IEquatable<GLRenderTarget>
{
    public const int VERTEX_DATA_LENGTH = 13 + 16;

    public Transform Transform { get; }
    public RenderOptions RenderOptions { get; }

    internal readonly VertexArrayObject<float, uint> VertexArrayObject;
    internal readonly BufferObject<Matrix4x4> ModelsBufferObject;

    internal readonly GLMaterial[] Materials;

    public unsafe GLRenderTarget(GL gl, Transform transform, VertexArrayObject<float, uint> vertexArrayObject, BufferObject<Matrix4x4> modelsBufferObject, GLMaterial[] materials, RenderOptions renderOptions)
    {
        Transform = transform;
        VertexArrayObject = vertexArrayObject;
        ModelsBufferObject = modelsBufferObject;
        Materials = materials;
        RenderOptions = renderOptions;

        VertexArrayObject.Bind();

        VertexArrayObject.VertexBufferObject.Bind();
        VertexArrayObject.SetVertexAttribute(0, 3, VertexAttribPointerType.Float, VERTEX_DATA_LENGTH, 0);
        VertexArrayObject.SetVertexAttribute(1, 4, VertexAttribPointerType.Float, VERTEX_DATA_LENGTH, 3);
        VertexArrayObject.SetVertexAttribute(2, 3, VertexAttribPointerType.Float, VERTEX_DATA_LENGTH, 7);
        VertexArrayObject.SetVertexAttribute(3, 3, VertexAttribPointerType.Float, VERTEX_DATA_LENGTH, 10);

        ModelsBufferObject.Bind();
        for (uint i = 0; i < 4; i++)
        {
            VertexArrayObject.SetVertexAttributePointer(4 + i, 4, VertexAttribPointerType.Float, (uint)sizeof(Matrix4x4), (int)(i * sizeof(float) * 4));
            VertexArrayObject.SetVertexAttributeDivisor(4 + i, 1);
        }

        for (var i = 0; i < Materials.Length; i++)
        {
            ShaderProgram shaderProgram = Materials[i].ShaderProgram;
            shaderProgram.BindAttributeLocation("in_position", 0);
            shaderProgram.BindAttributeLocation("in_color", 1);
            shaderProgram.BindAttributeLocation("in_uv", 2);
            shaderProgram.BindAttributeLocation("in_normal", 3);
            shaderProgram.BindAttributeLocation("model", 4);
        }

        gl.BindVertexArray(0);
    }

    protected override void OnDisposed()
    {
        //  TODO need to not dispose (potentially) shared objects.
        VertexArrayObject.VertexBufferObject.Dispose();
        VertexArrayObject.ElementBufferObject.Dispose();
        VertexArrayObject.Dispose();
        for (var i = 0; i < Materials.Length; i++)
        {
            Materials[i].Dispose();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(GLRenderTarget? other)
    {
        return Equals((object?)other);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj is not GLRenderTarget other)
        {
            return false;
        }

        if (!other.VertexArrayObject.Equals(VertexArrayObject))
        {
            return false;
        }

        if (!other.Materials.SequenceEqual(Materials))
        {
            return false;
        }

        return other.RenderOptions.Equals(RenderOptions);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(VertexArrayObject.GetHashCode(), Materials.Select(material => material.GetHashCode()).Aggregate(HashCode.Combine), RenderOptions.GetHashCode());
    }

    public override string ToString()
    {
        return base.ToString() + $"[{VertexArrayObject}]" + $"[{string.Join(',', Materials.Select(material => material.ToString()))}]";
    }
}

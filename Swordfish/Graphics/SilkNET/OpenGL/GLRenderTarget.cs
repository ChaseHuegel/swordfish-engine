using System.Numerics;
using System.Runtime.CompilerServices;
using Silk.NET.OpenGL;
using Swordfish.Library.Types;
using Swordfish.Util;

namespace Swordfish.Graphics.SilkNET.OpenGL;

internal sealed class GLRenderTarget : Handle, IRenderTarget, IEquatable<GLRenderTarget>
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
    public RenderOptions RenderOptions { get; set; }

    internal readonly VertexArrayObject<float, uint> VertexArrayObject;
    internal readonly BufferObject<Matrix4x4> ModelsBufferObject;

    internal readonly GLMaterial[] Materials;

    public unsafe GLRenderTarget(GL gl, Transform transform, VertexArrayObject<float, uint> vertexArrayObject, BufferObject<Matrix4x4> modelsBufferObject, GLMaterial[] materials, RenderOptions renderOptions)
    {
        GL = gl;
        Transform = transform;
        VertexArrayObject = vertexArrayObject;
        ModelsBufferObject = modelsBufferObject;
        Materials = materials;
        RenderOptions = renderOptions;

        VertexArrayObject.Bind();

        VertexArrayObject.VertexBufferObject.Bind();
        VertexArrayObject.SetVertexAttribute(0, 3, VertexAttribPointerType.Float, VertexDataLength, 0);
        VertexArrayObject.SetVertexAttribute(1, 4, VertexAttribPointerType.Float, VertexDataLength, 3);
        VertexArrayObject.SetVertexAttribute(2, 3, VertexAttribPointerType.Float, VertexDataLength, 7);
        VertexArrayObject.SetVertexAttribute(3, 3, VertexAttribPointerType.Float, VertexDataLength, 10);

        ModelsBufferObject.Bind();
        for (uint i = 0; i < 4; i++)
        {
            VertexArrayObject.SetVertexAttributePointer(4 + i, 4, VertexAttribPointerType.Float, (uint)sizeof(Matrix4x4), (int)(i * sizeof(float) * 4));
            VertexArrayObject.SetVertexAttributeDivisor(4 + i, 1);
        }

        for (int i = 0; i < Materials.Length; i++)
        {
            ShaderProgram shaderProgram = Materials[i].ShaderProgram;
            shaderProgram.BindAttributeLocation("in_position", 0);
            shaderProgram.BindAttributeLocation("in_color", 1);
            shaderProgram.BindAttributeLocation("in_uv", 2);
            shaderProgram.BindAttributeLocation("in_normal", 3);
            shaderProgram.BindAttributeLocation("model", 4);
        }

        GL.BindVertexArray(0);
    }

    protected override void OnDisposed()
    {
        //  TODO need to not dispose (potentially) shared objects.
        VertexArrayObject.VertexBufferObject.Dispose();
        VertexArrayObject.ElementBufferObject.Dispose();
        VertexArrayObject.Dispose();
        for (int i = 0; i < Materials.Length; i++)
            Materials[i].Dispose();
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
            return true;

        if (obj is not GLRenderTarget other)
            return false;

        if (!other.VertexArrayObject.Equals(VertexArrayObject))
            return false;

        if (!other.Materials.SequenceEqual(Materials))
            return false;

        if (!other.RenderOptions.Equals(RenderOptions))
            return false;

        return true;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(VertexArrayObject.GetHashCode(), Materials.Select(material => material.GetHashCode()).Aggregate(HashCode.Combine), RenderOptions.GetHashCode());
    }

    public override string? ToString()
    {
        return base.ToString() + $"[{VertexArrayObject}]" + $"[{string.Join(',', (object?[])Materials)}]";
    }
}

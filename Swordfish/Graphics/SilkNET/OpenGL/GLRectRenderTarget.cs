using System.Numerics;
using Swordfish.Library.Types;
using Swordfish.Types;

namespace Swordfish.Graphics.SilkNET.OpenGL;

internal sealed class GLRectRenderTarget : Handle, IEquatable<GLRectRenderTarget>
{
    public readonly Rect2 Rect;
    public readonly Vector4 Color;
    public readonly GLMaterial[] Materials;

    public GLRectRenderTarget(Rect2 rect, Vector4 color, GLMaterial[] materials)
    {
        Rect = rect;
        Color = color;
        Materials = materials;

        for (var i = 0; i < Materials.Length; i++)
        {
            ShaderProgram shaderProgram = Materials[i].ShaderProgram;
            shaderProgram.BindAttributeLocation("in_position", 0);
            shaderProgram.BindAttributeLocation("in_color", 1);
            shaderProgram.BindAttributeLocation("in_uv", 2);
        }
    }

    protected override void OnDisposed()
    {
    }

    public bool Equals(GLRectRenderTarget? other)
    {
        return Equals((object?)other);
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        return obj is GLRectRenderTarget other && other.Materials.SequenceEqual(Materials);
    }

    public override int GetHashCode()
    {
        return Materials.GetHashCode();
    }

    public override string ToString()
    {
        return base.ToString() + $"[{Rect}]" + $"[{string.Join(',', Materials.Select(material => material.ToString()))}]";
    }
}

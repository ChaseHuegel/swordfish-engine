using System.Numerics;
using Swordfish.Library.Types;
using Swordfish.Types;

namespace Swordfish.Graphics.SilkNET.OpenGL;

internal sealed class GLRectRenderTarget(in Rect2 rect, in Vector4 color, in GLMaterial[] materials)
    : Handle, IEquatable<GLRectRenderTarget>
{
    public readonly Rect2 Rect = rect;
    public readonly Vector4 Color = color;
    public readonly GLMaterial[] Materials = materials;

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

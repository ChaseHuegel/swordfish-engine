using System.Runtime.CompilerServices;
using Silk.NET.OpenGL;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.Types;

namespace Swordfish.Graphics.SilkNET.OpenGL;

internal sealed class GLMaterial : Handle
{
    public ShaderProgram ShaderProgram { get; }

    public IGLTexture[] Textures { get; }

    public bool Transparent { get; }

    public GLMaterial(ShaderProgram shaderProgram, IGLTexture[] textures, bool transparent)
    {
        ShaderProgram = shaderProgram;
        Textures = textures;
        Transparent = transparent;

        if (textures.Length > 32)
            throw new ArgumentException("Length can not exceed 32.", nameof(textures));
    }

    protected override void OnDisposed()
    {
        //  We do not want to dispose the shader and textures, they may be shared with other materials.
    }

    public void Use()
    {
        if (IsDisposed)
        {
            return;
        }

        ShaderProgram.Activate();

        for (int i = 0; i < Textures.Length; i++)
        {
            Textures[i].Activate(TextureUnit.Texture0 + i);
            ShaderProgram.SetUniform("texture" + i, i);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(GLMaterial? other)
    {
        if (other == null)
            return false;

        return Transparent.Equals(other.Transparent) && ShaderProgram.Equals(other.ShaderProgram) && Textures.SequenceEqual(other.Textures);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(this, obj))
            return true;

        if (obj is not GLMaterial other)
            return false;

        return Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Transparent.GetHashCode(), ShaderProgram.GetHashCode(), Textures.GetHashCode());
    }

    public override string? ToString()
    {
        return base.ToString() + $"[{ShaderProgram}]" + $"[{Textures.Length}]";
    }
}
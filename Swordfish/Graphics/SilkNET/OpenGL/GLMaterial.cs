using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using Silk.NET.OpenGL;
using Swordfish.Library.Diagnostics;

namespace Swordfish.Graphics.SilkNET.OpenGL;

internal sealed class GLMaterial : Handle
{
    public ShaderProgram ShaderProgram { get; }

    public IGLTexture[] Textures { get; }

    public GLMaterial(ShaderProgram shaderProgram, params IGLTexture[] textures)
    {
        ShaderProgram = shaderProgram;
        Textures = textures;

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
            Debugger.Log($"Attempted to use {this} but it is disposed.", LogType.ERROR);
            return;
        }

        ShaderProgram.Use();

        for (int i = 0; i < Textures.Length; i++)
        {
            Textures[i].Bind(TextureUnit.Texture0 + i);
            ShaderProgram.SetUniform("texture" + i, i);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(GLMaterial? other)
    {
        return ShaderProgram.Equals(other?.ShaderProgram) && Textures.SequenceEqual(other.Textures);
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
        return HashCode.Combine(ShaderProgram.GetHashCode(), Textures.GetHashCode());
    }

    public override string? ToString()
    {
        return base.ToString() + $"[{ShaderProgram}]" + $"[{Textures.Length}]";
    }
}
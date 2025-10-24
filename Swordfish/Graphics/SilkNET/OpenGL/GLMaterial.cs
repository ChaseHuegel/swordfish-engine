using System.Buffers;
using System.Runtime.CompilerServices;
using Silk.NET.OpenGL;
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
        {
            throw new ArgumentException("Length can not exceed 32.", nameof(textures));
        }
    }

    protected override void OnDisposed()
    {
        //  We do not want to dispose the shader and textures, they may be shared with other materials.
    }

    public Scope Use()
    {
        GLHandle.Scope[] scopes = ArrayPool<GLHandle.Scope>.Shared.Rent(Textures.Length + 1);
        
        scopes[0] = ShaderProgram.Use();

        for (var i = 0; i < Textures.Length; i++)
        {
            scopes[1 + i] = Textures[i].Activate(TextureUnit.Texture0 + i);
            ShaderProgram.SetUniform("texture" + i, i);
        }

        return new Scope(scopes);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(GLMaterial? other)
    {
        if (other == null)
        {
            return false;
        }

        return Transparent.Equals(other.Transparent) && ShaderProgram.Equals(other.ShaderProgram) && Textures.SequenceEqual(other.Textures);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        return obj is GLMaterial other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Transparent.GetHashCode(), ShaderProgram.GetHashCode(), Textures.GetHashCode());
    }

    public override string? ToString()
    {
        return base.ToString() + $"[{ShaderProgram}]" + $"[{Textures.Length}]";
    }

    public struct Scope : IDisposable
    {
        private GLHandle.Scope[]? _scopes;
        
        public Scope(GLHandle.Scope[] scopes)
        {
            _scopes = scopes;
        }

        public void Dispose()
        {
            if (_scopes == null)
            {
                return;
            }
            
            for (var i = 0; i < _scopes.Length; i++)
            {
                _scopes[i].Dispose();
            }
            
            ArrayPool<GLHandle.Scope>.Shared.Return(_scopes);
            _scopes = null;
        }
    }
}
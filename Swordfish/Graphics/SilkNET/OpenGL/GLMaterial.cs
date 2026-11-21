using System.Buffers;
using System.Runtime.CompilerServices;
using Silk.NET.OpenGL;
using Swordfish.Library.Types;

namespace Swordfish.Graphics.SilkNET.OpenGL;

internal sealed class GLMaterial : Handle
{
    public ShaderProgram ShaderProgram { get; }

    public IGLTexture[] Textures { get; }

    /// <summary>Alpha handling mode for this material.</summary>
    public AlphaMode AlphaMode { get; }

    /// <summary>Alpha discard threshold for masked rendering.</summary>
    public float AlphaCutoff { get; }

    public GLMaterial(ShaderProgram shaderProgram, IGLTexture[] textures, AlphaMode alphaMode, float alphaCutoff)
    {
        ShaderProgram = shaderProgram;
        Textures = textures;
        AlphaMode = alphaMode;
        AlphaCutoff = alphaCutoff;

        if (textures.Length > 32)
        {
            throw new ArgumentException("Length can not exceed 32.", nameof(textures));
        }
    }

    /// <summary>Resolves the effective alpha mode and cutoff from render options and materials.</summary>
    internal static void ResolveAlpha(in RenderOptions options, GLMaterial[] materials, out AlphaMode alphaMode, out float alphaCutoff)
    {
        if (options.AlphaMode != AlphaMode.Opaque)
        {
            alphaMode = options.AlphaMode;
            alphaCutoff = options.AlphaCutoff;
            return;
        }

        alphaMode = AlphaMode.Opaque;
        alphaCutoff = options.AlphaCutoff;

        for (var i = 0; i < materials.Length; i++)
        {
            GLMaterial material = materials[i];
            if (material.AlphaMode == AlphaMode.Blend)
            {
                alphaMode = AlphaMode.Blend;
                return;
            }

            if (material.AlphaMode == AlphaMode.Masked && alphaMode == AlphaMode.Opaque)
            {
                alphaMode = AlphaMode.Masked;
                alphaCutoff = material.AlphaCutoff;
            }
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

        return new Scope(new ArraySegment<GLHandle.Scope>(scopes, 0, Textures.Length));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(GLMaterial? other)
    {
        if (other == null)
        {
            return false;
        }

        return AlphaMode == other.AlphaMode
            && AlphaCutoff.Equals(other.AlphaCutoff)
            && ShaderProgram.Equals(other.ShaderProgram)
            && Textures.SequenceEqual(other.Textures);
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
        return HashCode.Combine(AlphaMode.GetHashCode(), AlphaCutoff.GetHashCode(), ShaderProgram.GetHashCode(), Textures.GetHashCode());
    }

    public override string? ToString()
    {
        return base.ToString() + $"[{ShaderProgram}]" + $"[{Textures.Length}]";
    }

    public struct Scope : IDisposable
    {
        private ArraySegment<GLHandle.Scope>? _scopes;
        
        public Scope(ArraySegment<GLHandle.Scope> scopes)
        {
            _scopes = scopes;
        }

        public void Dispose()
        {
            if (_scopes == null)
            {
                return;
            }
            
            for (var i = 0; i < _scopes.Value.Count; i++)
            {
                _scopes.Value[i].Dispose();
            }
            
            ArrayPool<GLHandle.Scope>.Shared.Return(_scopes.Value.Array!);
            _scopes = null;
        }
    }
}
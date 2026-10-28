using System.Runtime.CompilerServices;
using Silk.NET.OpenGL;

namespace Swordfish.Graphics.SilkNET.OpenGL;

/// <summary>Represents an OpenGL cubemap array texture.</summary>
internal sealed class TexCubeArray : GLHandle, IGLTexture<TexCubeArray>
{
    public string Name { get; }
    public uint Width { get; private set; }
    public uint Height { get; private set; }
    public uint Layers { get; private set; }

    private readonly GL _gl;
    private readonly TextureFormat _format;
    private readonly TextureParams _params;

    public unsafe TexCubeArray(
        GL gl,
        string name,
        uint width,
        uint height,
        uint cubeCount,
        TextureFormat format,
        TextureParams @params
    ) {
        _gl = gl;
        Name = name;
        Width = width;
        Height = height;
        Layers = cubeCount * 6;
        _format = format;
        _params = @params;

        using Scope _ = Use();
        _gl.TexImage3D(TextureTarget.TextureCubeMapArrayExt, 0, format.InternalFormat, width, height, Layers, border: 0, format.PixelFormat, format.PixelType, null);
        _gl.TexParameter(TextureTarget.TextureCubeMapArrayExt, TextureParameterName.TextureWrapS, (int)@params.WrapS);
        _gl.TexParameter(TextureTarget.TextureCubeMapArrayExt, TextureParameterName.TextureWrapT, (int)@params.WrapT);
        _gl.TexParameter(TextureTarget.TextureCubeMapArrayExt, TextureParameterName.TextureWrapR, (int)TextureWrapMode.ClampToEdge);
        _gl.TexParameter(TextureTarget.TextureCubeMapArrayExt, TextureParameterName.TextureMinFilter, (int)@params.MinFilter);
        _gl.TexParameter(TextureTarget.TextureCubeMapArrayExt, TextureParameterName.TextureMagFilter, (int)@params.MagFilter);
        _gl.TexParameter(TextureTarget.TextureCubeMapArrayExt, TextureParameterName.TextureCompareMode, (int)(@params.CompareMode ? TextureCompareMode.CompareRefToTexture : TextureCompareMode.None));

        if (@params.CompareMode)
        {
            _gl.TexParameter(TextureTarget.TextureCubeMapArrayExt, TextureParameterName.TextureCompareFunc, (int)DepthFunction.Lequal);
        }
    }

    public unsafe void Resize(uint width, uint height, uint cubeCount, TextureParams? newParams = null)
    {
        Width = width;
        Height = height;
        Layers = cubeCount * 6;

        TextureParams @params = newParams ?? _params;

        using Scope _ = Use();
        _gl.TexImage3D(TextureTarget.TextureCubeMapArrayExt, 0, _format.InternalFormat, width, height, Layers, border: 0, _format.PixelFormat, _format.PixelType, null);
        _gl.TexParameter(TextureTarget.TextureCubeMapArrayExt, TextureParameterName.TextureWrapS, (int)@params.WrapS);
        _gl.TexParameter(TextureTarget.TextureCubeMapArrayExt, TextureParameterName.TextureWrapT, (int)@params.WrapT);
        _gl.TexParameter(TextureTarget.TextureCubeMapArrayExt, TextureParameterName.TextureWrapR, (int)TextureWrapMode.ClampToEdge);
        _gl.TexParameter(TextureTarget.TextureCubeMapArrayExt, TextureParameterName.TextureMinFilter, (int)@params.MinFilter);
        _gl.TexParameter(TextureTarget.TextureCubeMapArrayExt, TextureParameterName.TextureMagFilter, (int)@params.MagFilter);
        _gl.TexParameter(TextureTarget.TextureCubeMapArrayExt, TextureParameterName.TextureCompareMode, (int)(@params.CompareMode ? TextureCompareMode.CompareRefToTexture : TextureCompareMode.None));

        if (@params.CompareMode)
        {
            _gl.TexParameter(TextureTarget.TextureCubeMapArrayExt, TextureParameterName.TextureCompareFunc, (int)DepthFunction.Lequal);
        }
    }

    protected override uint CreateHandle() => _gl.GenTexture();

    protected override void FreeHandle() => _gl.DeleteTexture(Handle);

    protected override void BindHandle() => _gl.BindTexture(TextureTarget.TextureCubeMapArrayExt, Handle);

    protected override void UnbindHandle() => _gl.BindTexture(TextureTarget.TextureCubeMapArrayExt, 0);

    public Scope Activate(TextureUnit textureSlot)
    {
        _gl.ActiveTexture(textureSlot);
        return Use();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(TexCubeArray? other) => Handle.Equals(other?.Handle);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(object? obj) => ReferenceEquals(this, obj) || (obj is TexCubeArray other && Equals(other));

    public override int GetHashCode() => (int)Handle;
}

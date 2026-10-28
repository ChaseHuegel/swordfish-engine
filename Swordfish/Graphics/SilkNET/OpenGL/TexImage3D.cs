using System.Runtime.CompilerServices;
using Silk.NET.OpenGL;

namespace Swordfish.Graphics.SilkNET.OpenGL;

/// <summary>Represents an OpenGL 2D array or 3D texture.</summary>
internal sealed class TexImage3D : GLHandle, IGLTexture<TexImage3D>
{
    public string Name { get; }
    public uint Width { get; private set; }
    public uint Height { get; private set; }
    public uint Depth { get; private set; }

    private readonly GL _gl;
    private readonly TextureFormat _format;
    private readonly TextureParams _params;

    public unsafe TexImage3D(
        GL gl,
        string name,
        byte* pixels,
        uint width,
        uint height,
        uint depth,
        TextureFormat format,
        TextureParams @params
    ) {
        _gl = gl;
        Name = name;
        Width = width;
        Height = height;
        Depth = depth;
        _format = format;
        _params = @params;

        using Scope _ = Use();
        _gl.TexImage3D(TextureTarget.Texture2DArray, 0, format.InternalFormat, width, height, depth, border: 0, format.PixelFormat, format.PixelType, pixels);
        _gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapS, (int)@params.WrapS);
        _gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapT, (int)@params.WrapT);
        _gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMinFilter, (int)@params.MinFilter);
        _gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMagFilter, (int)@params.MagFilter);
        _gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureCompareMode, (int)(@params.CompareMode ? TextureCompareMode.CompareRefToTexture : TextureCompareMode.None));

        if (@params.CompareMode)
        {
            _gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureCompareFunc, (int)DepthFunction.Lequal);
        }

        if (@params.GenerateMipmaps)
        {
            _gl.GenerateMipmap(TextureTarget.Texture2DArray);
        }
    }

    public unsafe void Resize(uint width, uint height, uint depth, TextureParams? newParams = null)
    {
        Width = width;
        Height = height;
        Depth = depth;

        TextureParams @params = newParams ?? _params;

        using Scope _ = Use();
        _gl.TexImage3D(TextureTarget.Texture2DArray, 0, _format.InternalFormat, width, height, depth, border: 0, _format.PixelFormat, _format.PixelType, null);
        _gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapS, (int)@params.WrapS);
        _gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapT, (int)@params.WrapT);
        _gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMinFilter, (int)@params.MinFilter);
        _gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMagFilter, (int)@params.MagFilter);
        _gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureCompareMode, (int)(@params.CompareMode ? TextureCompareMode.CompareRefToTexture : TextureCompareMode.None));

        if (@params.CompareMode)
        {
            _gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureCompareFunc, (int)DepthFunction.Lequal);
        }
    }

    protected override uint CreateHandle()
    {
        return _gl.GenTexture();
    }

    protected override void FreeHandle()
    {
        _gl.DeleteTexture(Handle);
    }

    protected override void BindHandle()
    {
        _gl.BindTexture(TextureTarget.Texture2DArray, Handle);
    }

    protected override void UnbindHandle()
    {
        _gl.BindTexture(TextureTarget.Texture2DArray, 0);
    }

    public Scope Activate(TextureUnit textureSlot)
    {
        _gl.ActiveTexture(textureSlot);
        return Use();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(TexImage3D? other)
    {
        return Handle.Equals(other?.Handle);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        return obj is TexImage3D other && Equals(other);
    }

    public override int GetHashCode()
    {
        return (int)Handle;
    }

    public override string? ToString()
    {
        return base.ToString() + $"[{Handle}]";
    }
}

using System.Runtime.CompilerServices;
using Silk.NET.OpenGL;

namespace Swordfish.Graphics.SilkNET.OpenGL;

internal sealed class TexCubemap : GLHandle, IGLTexture<TexCubemap>
{
    public string Name { get; }
    
    public uint Width { get; }
    public uint Height { get; }

    private readonly GL _gl;
    private readonly TextureFormat _format;
    private readonly TextureParams _params;

    public unsafe TexCubemap(
        GL gl,
        string name,
        byte*[] pixels,
        uint width,
        uint height,
        TextureFormat format,
        TextureParams @params
    ) {
        _gl = gl;
        Name = name;
        Width = width;
        Height = height;
        _format = format;
        _params = @params;

        using Scope _ = Use();

        if (pixels.Length != 6)
        {
            throw new GLException("Cubemaps require 6 textures.");
        }
        
        for (var i = 0; i < 6; i++)
        {
            _gl.TexImage2D(TextureTarget.TextureCubeMapPositiveX + i, 0, format.InternalFormat, width, height, border: 0, format.PixelFormat, format.PixelType, pixels[i]);
        }

        _gl.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapS, (int)@params.WrapS);
        _gl.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapT, (int)@params.WrapT);
        _gl.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapR, (int)TextureWrapMode.ClampToEdge);
        _gl.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMinFilter, (int)@params.MinFilter);
        _gl.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMagFilter, (int)@params.MagFilter);

        if (@params.GenerateMipmaps)
        {
            _gl.GenerateMipmap(TextureTarget.TextureCubeMap);
        }
    }
    
    public unsafe void UpdateData(uint width, uint height, byte*[] pixels)
    {
        using Scope _ = Use();
        
        if (pixels.Length != 6)
        {
            throw new GLException("Cubemaps require 6 textures.");
        }
        
        for (var i = 0; i < 6; i++)
        {
            _gl.TexImage2D(TextureTarget.TextureCubeMapPositiveX + i, 0, _format.InternalFormat, width, height, border: 0, _format.PixelFormat, _format.PixelType, pixels[i]);
        }
        
        if (_params.GenerateMipmaps)
        {
            _gl.GenerateMipmap(TextureTarget.TextureCubeMap);
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
        _gl.BindTexture(TextureTarget.TextureCubeMap, Handle);
    }

    protected override void UnbindHandle()
    {
        _gl.BindTexture(TextureTarget.TextureCubeMap, 0);
    }

    public Scope Activate(TextureUnit textureSlot)
    {
        _gl.ActiveTexture(textureSlot);
        return Use();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(TexCubemap? other)
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

        return obj is TexCubemap other && Equals(other);
    }

    public override int GetHashCode()
    {
        return (int)Handle;
    }

    public override string ToString()
    {
        return base.ToString() + $"[{Handle}]";
    }
}

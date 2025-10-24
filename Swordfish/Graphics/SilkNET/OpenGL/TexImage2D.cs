using System.Runtime.CompilerServices;
using Silk.NET.OpenGL;

namespace Swordfish.Graphics.SilkNET.OpenGL;

internal sealed class TexImage2D : GLHandle, IGLTexture<TexImage2D>
{
    public string Name { get; }
    
    public uint Width { get; }
    public uint Height { get; }

    private readonly GL _gl;
    private readonly TextureFormat _format;
    private readonly TextureParams _params;

    public unsafe TexImage2D(
        GL gl,
        string name,
        byte* pixels,
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
        _gl.TexImage2D(TextureTarget.Texture2D, 0, format.InternalFormat, width, height, border: 0, format.PixelFormat, format.PixelType, pixels);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)@params.WrapS);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)@params.WrapT);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)@params.MinFilter);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)@params.MagFilter);

        if (@params.GenerateMipmaps)
        {
            _gl.GenerateMipmap(TextureTarget.Texture2D);
        }
    }
    
    public unsafe void UpdateData(uint width, uint height, byte* pixels)
    {
        using Scope _ = Use();
        _gl.TexImage2D(TextureTarget.Texture2D, 0, _format.InternalFormat, width, height, border: 0, _format.PixelFormat, _format.PixelType, pixels);
        
        if (_params.GenerateMipmaps)
        {
            _gl.GenerateMipmap(TextureTarget.Texture2D);
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
        _gl.BindTexture(TextureTarget.Texture2D, Handle);
    }

    protected override void UnbindHandle()
    {
        _gl.BindTexture(TextureTarget.Texture2D, 0);
    }

    public Scope Activate(TextureUnit textureSlot)
    {
        _gl.ActiveTexture(textureSlot);
        return Use();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(TexImage2D? other)
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

        return obj is TexImage2D other && Equals(other);
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

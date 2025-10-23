using System.Runtime.CompilerServices;
using Silk.NET.OpenGL;

namespace Swordfish.Graphics.SilkNET.OpenGL;

internal sealed class TexImage3D : GLHandle, IGLTexture<TexImage3D>
{
    public string Name { get; }

    private readonly GL _gl;
    
    public unsafe TexImage3D(
        GL gl,
        string name,
        byte* pixels,
        uint width,
        uint height, 
        uint depth
    ) : this(gl, name, pixels, width, height, depth, TextureFormat.RgbaByte, TextureParams.ClampNearest) { }

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

        Activate();

        _gl.TexImage3D(TextureTarget.Texture2DArray, 0, format.InternalFormat, width, height, depth, border: 0, format.PixelFormat, format.PixelType, pixels);
        _gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapS, (int)@params.WrapS);
        _gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapT, (int)@params.WrapT);
        _gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMinFilter, (int)@params.MinFilter);
        _gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMagFilter, (int)@params.MagFilter);

        if (@params.GenerateMipmaps)
        {
            _gl.GenerateMipmap(TextureTarget.Texture2DArray);
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

    public void Activate(TextureUnit textureSlot = TextureUnit.Texture0)
    {
        if (IsDisposed)
        {
            return;
        }

        _gl.ActiveTexture(textureSlot);
        Bind();
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

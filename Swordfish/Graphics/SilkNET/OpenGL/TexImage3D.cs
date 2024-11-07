using System.Runtime.CompilerServices;
using Silk.NET.OpenGL;

namespace Swordfish.Graphics.SilkNET.OpenGL;

internal sealed class TexImage3D : GLHandle, IGLTexture<TexImage3D>
{
    public string Name { get; }

    private readonly GL _gl;
    // ReSharper disable once NotAccessedField.Local
    private readonly byte _mipmapLevels; //  TODO implement mipmaps

    public unsafe TexImage3D(GL gl, string name, byte* pixels, uint width, uint height, uint depth, bool generateMipmaps)
    {
        _gl = gl;
        Name = name;
        _mipmapLevels = generateMipmaps == false ? (byte)0 : (byte)Math.Floor(Math.Log(Math.Max(width, height), 2));

        Activate();

        //  TODO introduce texture options. ie. need to be able to specify Srgba[Alpha]
        _gl.TexImage3D(TextureTarget.Texture2DArray, 0, InternalFormat.Rgba, width, height, depth, 0, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);
        SetDefaultParameters();
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

    private void SetDefaultParameters()
    {
        _gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
        _gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
        _gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureBaseLevel, 0);
        _gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMaxLevel, 0);
        _gl.GenerateMipmap(TextureTarget.Texture2DArray);
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

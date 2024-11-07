using System.Runtime.CompilerServices;
using Silk.NET.OpenGL;

namespace Swordfish.Graphics.SilkNET.OpenGL;

internal sealed class TexImage3D : GLHandle, IGLTexture<TexImage3D>
{
    public string Name { get; private set; }

    private readonly GL GL;
    private readonly byte MipmapLevels;

    public unsafe TexImage3D(GL gl, string name, byte* pixels, uint width, uint height, uint depth, bool generateMipmaps)
    {
        GL = gl;
        Name = name;
        MipmapLevels = generateMipmaps == false ? (byte)0 : (byte)Math.Floor(Math.Log(Math.Max(width, height), 2));

        Activate();

        //  TODO introduce texture options. ie. need to be able to specify Srgba[Alpha]
        GL.TexImage3D(TextureTarget.Texture2DArray, 0, InternalFormat.Rgba, width, height, depth, 0, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);
        SetDefaultParameters();
    }

    protected override uint CreateHandle()
    {
        return GL.GenTexture();
    }

    protected override void FreeHandle()
    {
        GL.DeleteTexture(Handle);
    }

    protected override void BindHandle()
    {
        GL.BindTexture(TextureTarget.Texture2DArray, Handle);
    }

    protected override void UnbindHandle()
    {
        GL.BindTexture(TextureTarget.Texture2DArray, 0);
    }

    public void Activate(TextureUnit textureSlot = TextureUnit.Texture0)
    {
        if (IsDisposed)
        {
            return;
        }

        GL.ActiveTexture(textureSlot);
        Bind();
    }

    private void SetDefaultParameters()
    {
        GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureBaseLevel, 0);
        GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMaxLevel, 0);
        GL.GenerateMipmap(TextureTarget.Texture2DArray);
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
            return true;

        if (obj is not TexImage3D other)
            return false;

        return Equals(other);
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

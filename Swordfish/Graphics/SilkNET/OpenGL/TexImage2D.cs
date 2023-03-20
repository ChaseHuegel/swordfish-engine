using Silk.NET.OpenGL;
using Swordfish.Library.Diagnostics;

namespace Swordfish.Graphics.SilkNET.OpenGL;

internal sealed class TexImage2D : ManagedHandle<uint>, IEquatable<TexImage2D>
{
    public string Name { get; private set; }

    private readonly GL GL;
    private readonly byte MipmapLevels;

    public unsafe TexImage2D(GL gl, string name, byte* pixels, uint width, uint height, bool generateMipmaps)
    {
        GL = gl;
        Name = name;
        MipmapLevels = generateMipmaps == false ? (byte)0 : (byte)Math.Floor(Math.Log(Math.Max(width, height), 2));

        Bind();

        GL.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, width, height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);
        SetDefaultParameters();
    }

    protected override uint CreateHandle()
    {
        return GL.GenTexture();
    }

    protected override void OnDisposed()
    {
        GL.DeleteTexture(Handle);
    }

    public void Bind(TextureUnit textureSlot = TextureUnit.Texture0)
    {
        if (IsDisposed)
        {
            Debugger.Log($"Attempted to bind {this} but it is disposed.", LogType.ERROR);
            return;
        }

        GL.ActiveTexture(textureSlot);
        GL.BindTexture(TextureTarget.Texture2D, Handle);
    }

    private void SetDefaultParameters()
    {
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBaseLevel, 0);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, 0);
        GL.GenerateMipmap(TextureTarget.Texture2D);
    }

    public bool Equals(TexImage2D? other)
    {
        return Handle.Equals(other?.Handle);
    }

    public override bool Equals(object? obj)
    {
        if (obj is not TexImage2D other)
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

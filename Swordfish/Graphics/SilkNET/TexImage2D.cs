using Silk.NET.OpenGL;
using Swordfish.Library.Diagnostics;

namespace Swordfish.Graphics.SilkNET;

internal sealed class TexImage2D : IDisposable
{
    public string Name { get; private set; }

    private readonly GL GL;
    private readonly uint Handle;
    private readonly byte MipmapLevels;

    private volatile bool Disposed;

    public unsafe TexImage2D(GL gl, string name, byte* pixels, uint width, uint height, bool generateMipmaps)
    {
        GL = gl;
        Name = name;
        Handle = GL.GenTexture();
        MipmapLevels = generateMipmaps == false ? (byte)0 : (byte)Math.Floor(Math.Log(Math.Max(width, height), 2));

        Bind();

        GL.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, width, height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);
        SetDefaultParameters();
    }

    public void Dispose()
    {
        if (Disposed)
        {
            Debugger.Log($"Attempted to dispose {this} but it is already disposed.", LogType.WARNING);
            return;
        }

        Disposed = true;
        GL.DeleteTexture(Handle);
    }

    public void Bind(TextureUnit textureSlot = TextureUnit.Texture0)
    {
        if (Disposed)
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
}

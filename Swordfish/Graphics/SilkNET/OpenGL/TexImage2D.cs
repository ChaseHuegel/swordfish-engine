using System;
using System.Runtime.CompilerServices;
using Silk.NET.OpenGL;
using Swordfish.Library.Diagnostics;

namespace Swordfish.Graphics.SilkNET.OpenGL;

internal sealed class TexImage2D : GLHandle, IGLTexture<TexImage2D>
{
    public string Name { get; private set; }

    private readonly GL GL;
    private readonly byte MipmapLevels;

    public unsafe TexImage2D(GL gl, string name, byte* pixels, uint width, uint height, bool generateMipmaps)
    {
        GL = gl;
        Name = name;
        MipmapLevels = generateMipmaps == false ? (byte)0 : (byte)Math.Floor(Math.Log(Math.Max(width, height), 2));

        Activate();

        //  TODO introduce texture options. ie. need to be able to specify Srgba[Alpha]
        GL.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, width, height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);
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
        GL.BindTexture(TextureTarget.Texture2D, Handle);
    }

    protected override void UnbindHandle()
    {
        GL.BindTexture(TextureTarget.Texture2D, 0);
    }

    public void Activate(TextureUnit textureSlot = TextureUnit.Texture0)
    {
        if (IsDisposed)
        {
            Debugger.Log($"Attempted to use {this} but it is disposed.", LogType.ERROR);
            return;
        }

        GL.ActiveTexture(textureSlot);
        Bind();
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(TexImage2D? other)
    {
        return Handle.Equals(other?.Handle);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(this, obj))
            return true;

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

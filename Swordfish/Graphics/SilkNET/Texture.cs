using Silk.NET.OpenGL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.IO;

namespace Swordfish.Graphics.SilkNET;

public sealed class Texture : IDisposable
{
    private static GL GL => gl ??= SwordfishEngine.Kernel.Get<GL>();
    private static GL? gl;

    private uint Handle;
    private byte MipmapLevels;

    private volatile bool Disposed;

    public unsafe Texture(Span<byte> pixels, uint width, uint height, bool generateMipmaps = false)
    {
        Handle = GL.GenTexture();
        MipmapLevels = generateMipmaps == false ? (byte)0 : (byte)Math.Floor(Math.Log(Math.Max(width, height), 2));

        Bind();

        fixed (void* pixelPtr = pixels)
        {
            GL.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, width, height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, pixelPtr);
            SetDefaultParameters();
        }
    }

    private struct TextureArgs
    {
        public Image<Rgba32> image;
        public bool generateMipmaps;

        public TextureArgs(Image<Rgba32> image, bool generateMipmaps)
        {
            this.image = image;
            this.generateMipmaps = generateMipmaps;
        }
    }

    public unsafe Texture(Image<Rgba32> image, bool generateMipmaps = false)
    {
        SwordfishEngine.WaitForMainThread(Construct, new TextureArgs(image, generateMipmaps));
    }

    private unsafe void Construct(TextureArgs textureArgs)
    {
        Image<Rgba32> image = textureArgs.image;
        bool generateMipmaps = textureArgs.generateMipmaps;

        Handle = GL.GenTexture();
        MipmapLevels = generateMipmaps == false ? (byte)0 : (byte)Math.Floor(Math.Log(Math.Max(image.Width, image.Height), 2));

        Bind();

        GL.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, (uint)image.Width, (uint)image.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, null);
        image.ProcessPixelRows(SubImagePixelRow);

        static void SubImagePixelRow(PixelAccessor<Rgba32> pixelAccessor)
        {
            for (int y = 0; y < pixelAccessor.Height; y++)
            {
                fixed (void* data = pixelAccessor.GetRowSpan(y))
                {
                    GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, y, (uint)pixelAccessor.Width, 1, PixelFormat.Rgba, PixelType.UnsignedByte, data);
                }
            }
        }

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

    public unsafe static Texture LoadFrom(IPath path)
    {
        IFileService fileService = SwordfishEngine.Kernel.Get<IFileService>();

        using Stream stream = fileService.Read(path);
        using StreamReader reader = new(stream);
        using Image<Rgba32> image = Image.Load<Rgba32>(stream);
        return new Texture(image);
    }
}

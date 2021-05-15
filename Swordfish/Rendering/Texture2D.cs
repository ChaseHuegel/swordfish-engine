using System;
using System.Drawing;
using System.Drawing.Imaging;
using OpenTK.Graphics.OpenGL4;
using PixelFormat = OpenTK.Graphics.OpenGL4.PixelFormat;
using Swordfish;
using System.IO;

namespace Swordfish.Rendering
{
    public enum TextureCoordinate
    {
        S = TextureParameterName.TextureWrapS,
        T = TextureParameterName.TextureWrapT,
        R = TextureParameterName.TextureWrapR
    }

    public class Texture2D
    {
        public readonly string Name;
        public readonly int Handle;
        public readonly byte MipmapLevels;

        public static readonly float MaxAniso;
        static Texture2D() { MaxAniso = GL.GetFloat((GetPName)0x84FF); }

        public static Texture2D LoadFromFile(string path, string name, bool generateMipmaps = true)
        {
            int handle = GL.GenTexture();

            Bitmap image = new Bitmap(path);

            GL.BindTexture(TextureTarget.Texture2D, handle);
            BitmapData data = image.LockBits(new System.Drawing.Rectangle(0, 0, image.Width, image.Height),
                ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, data.Width, data.Height, 0,
                PixelFormat.Bgra, PixelType.UnsignedByte, data.Scan0);

            image.UnlockBits(data);

            if (generateMipmaps) GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);

            return new Texture2D(handle, name, image.Width, image.Height, generateMipmaps);
        }

        public Texture2D(int handle, string name, int width, int height, bool generateMipmaps = true)
        {
            MipmapLevels = generateMipmaps == false ? (byte)1 : (byte)Math.Floor(Math.Log(Math.Max(width, height), 2));
            Handle = handle;
            Name = name;
        }

        public Texture2D(string name, int width, int height, IntPtr data, bool generateMipmaps = true)
        {
            MipmapLevels = generateMipmaps == false ? (byte)1 : (byte)Math.Floor(Math.Log(Math.Max(width, height), 2));
            Name = name;

            GL.CreateTextures(TextureTarget.Texture2D, 1, out Handle);
            GL.TextureStorage2D(Handle, MipmapLevels, SizedInternalFormat.Rgba8, width, height);
            GL.TextureSubImage2D(Handle, 0, 0, 0, width, height, PixelFormat.Bgra, PixelType.UnsignedByte, data);

            if (generateMipmaps) GL.GenerateTextureMipmap(Handle);

            GL.TextureParameter(Handle, TextureParameterName.TextureMaxLevel, MipmapLevels - 1);
        }

        public virtual void Use(TextureUnit unit)
        {
            GL.ActiveTexture(unit);
            GL.BindTexture(TextureTarget.Texture2D, Handle);
        }

        public void SetMinFilter(TextureMinFilter filter)
        {
            GL.TextureParameter(Handle, TextureParameterName.TextureMinFilter, (int)filter);
        }

        public void SetMagFilter(TextureMagFilter filter)
        {
            GL.TextureParameter(Handle, TextureParameterName.TextureMagFilter, (int)filter);
        }

        public void SetAnisotropy(float level)
        {
            const TextureParameterName TEXTURE_MAX_ANISOTROPY = (TextureParameterName)0x84FE;
            GL.TextureParameter(Handle, TEXTURE_MAX_ANISOTROPY, Math.Clamp(level, 1, MaxAniso));
        }

        public void SetLod(int @base, int min, int max)
        {
            GL.TextureParameter(Handle, TextureParameterName.TextureLodBias, @base);
            GL.TextureParameter(Handle, TextureParameterName.TextureMinLod, min);
            GL.TextureParameter(Handle, TextureParameterName.TextureMaxLod, max);
        }

        public void SetWrap(TextureCoordinate coord, TextureWrapMode mode)
        {
            GL.TextureParameter(Handle, (TextureParameterName)coord, (int)mode);
        }

        public void Dispose()
        {
            GL.DeleteTexture(Handle);
        }
    }
}
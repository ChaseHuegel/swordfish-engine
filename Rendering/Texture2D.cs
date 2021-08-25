using System;
using System.Drawing;
using System.Drawing.Imaging;

using OpenTK.Graphics.OpenGL4;

using Swordfish.Diagnostics;

using PixelFormat = OpenTK.Graphics.OpenGL4.PixelFormat;

namespace Swordfish.Rendering
{
    public class Texture2D : Texture
    {
        public static Texture2D LoadFromFile(string path, string name, bool generateMipmaps = true)
        {
            int handle = GL.GenTexture();

            Debug.Log($"Loading texture '{name}' from '{path}'");

            Bitmap image = new Bitmap(path);
            image.RotateFlip(RotateFlipType.RotateNoneFlipY);

            GL.BindTexture(TextureTarget.Texture2D, handle);
            BitmapData data = image.LockBits(new System.Drawing.Rectangle(0, 0, image.Width, image.Height),
                ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.SrgbAlpha, data.Width, data.Height, 0,
                PixelFormat.Bgra, PixelType.UnsignedByte, data.Scan0);

            image.UnlockBits(data);

            if (generateMipmaps) GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);

            return new Texture2D(handle, name, image.Width, image.Height, generateMipmaps);
        }

        public Texture2D(int handle, string name, int width, int height, bool generateMipmaps = true)
        {
            mipmapLevels = generateMipmaps == false ? (byte)1 : (byte)Math.Floor(Math.Log(Math.Max(width, height), 2));

            base.handle = handle;
            base.name = name;
        }

        public Texture2D(string name, int width, int height, IntPtr data, bool generateMipmaps = true)
        {
            mipmapLevels = generateMipmaps == false ? (byte)1 : (byte)Math.Floor(Math.Log(Math.Max(width, height), 2));
            base.name = name;

            GL.CreateTextures(TextureTarget.Texture2D, 1, out int handleOut);
            handle = handleOut;

            GL.TextureStorage2D(handle, mipmapLevels, SizedInternalFormat.Rgba8, width, height);
            GL.TextureSubImage2D(handle, 0, 0, 0, width, height, PixelFormat.Bgra, PixelType.UnsignedByte, data);

            if (generateMipmaps) GL.GenerateTextureMipmap(handle);

            Debug.Log($"Created texture '{name}'");

            GL.TextureParameter(handle, TextureParameterName.TextureMaxLevel, mipmapLevels - 1);
        }
    }
}
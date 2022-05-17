using System;
using System.Drawing;
using System.Drawing.Imaging;

using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

using Swordfish.Library.Diagnostics;

using PixelFormat = OpenTK.Graphics.OpenGL4.PixelFormat;

namespace Swordfish.Engine.Rendering
{
    /// <summary>
    /// A simple image representation intended for UI elements
    /// <para/> Does not support mipmaps, flip openGL UV, or apply color corrections
    /// </summary>
    public class Image2D : Texture
    {
        public readonly Bitmap bitmap;

        public static Image2D LoadFromFile(string path, string name)
        {
            int handle = GL.GenTexture();

            Debug.Log($"Loading image '{name}' from '{path}'");

            Bitmap image = new Bitmap(path);

            GL.BindTexture(TextureTarget.Texture2D, handle);
            BitmapData data = image.LockBits(new System.Drawing.Rectangle(0, 0, image.Width, image.Height),
                ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            //  Use RGBA, do not apply color correction
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, data.Width, data.Height, 0,
                PixelFormat.Bgra, PixelType.UnsignedByte, data.Scan0);

            image.UnlockBits(data);

            return new Image2D(handle, name, image.Width, image.Height, image);
        }

        public Image2D(int handle, string name, int width, int height, Bitmap bitmap = null)
        {
            mipmapLevels = 1;

            base.handle = handle;
            base.name = name;

            size = new Vector2(width, height);
            this.bitmap = bitmap;
        }

        public Image2D(string name, int width, int height, IntPtr data, Bitmap bitmap = null)
        {
            mipmapLevels = 1;
            base.name = name;

            GL.CreateTextures(TextureTarget.Texture2D, 1, out int handleOut);
            handle = handleOut;

            GL.TextureStorage2D(handle, mipmapLevels, SizedInternalFormat.Rgba8, width, height);
            GL.TextureSubImage2D(handle, 0, 0, 0, width, height, PixelFormat.Bgra, PixelType.UnsignedByte, data);

            size = new Vector2(width, height);
            this.bitmap = bitmap;

            Debug.Log($"Created image '{name}'");

            GL.TextureParameter(handle, TextureParameterName.TextureMaxLevel, mipmapLevels - 1);
        }
    }
}
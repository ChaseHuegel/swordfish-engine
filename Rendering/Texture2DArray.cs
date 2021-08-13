using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

using OpenTK.Graphics.OpenGL4;

using Swordfish.Diagnostics;
using Swordfish.Util;

using PixelFormat = OpenTK.Graphics.OpenGL4.PixelFormat;

namespace Swordfish.Rendering
{
    public class Texture2DArray : Texture
    {
        private static bool IsValidBitmap(Bitmap bitmap)
        {
            return bitmap.Width == bitmap.Height;
        }

        public static Texture2DArray CreateFromFolder(string path, string name, int width = 0, int height = 0, bool generateMipmaps = true)
        {
            int handle = GL.GenTexture();
            int numOfLayers = 0;
            bool useLargestRes = (width <= 0 || height <= 0);

            Debug.Log($"Loading texture array '{name}' from '{path}'");

            Bitmap bitmap;
            List<Bitmap> images = new List<Bitmap>();
            DirectoryInfo directory = new DirectoryInfo(path);

            //  Find all valid textures
            foreach (FileInfo file in directory.GetFiles("*.png"))
            {
                bitmap = new Bitmap(file.FullName);

                //  Check the bitmap is a valid texture
                if (!IsValidBitmap(bitmap)) continue;

                images.Add(bitmap);
                Debug.Log($"    Found texture '{file.Name}' at {numOfLayers}");

                //  Use the largest res if there wasn't one set
                if (useLargestRes && bitmap.Width > width)
                    height = width = bitmap.Width;

                //  Increase # of layers if this was a valid texture
                numOfLayers++;
            }
            Debug.Log($"    ...layers: {numOfLayers}");

            //  Resize any images that aren't using the correct res
            Debug.Log($"    ...using resolution {width}x{height}");
            for (int i = 0; i < images.Count; i++)
                if (images[i].Width != width)
                    images[i] = Gfx.ResizeBitmap(images[i], width, height);

            Debug.Log($"...Building texture array '{name}'");
            GL.BindTexture(TextureTarget.Texture2DArray, handle);
            GL.TexImage3D(TextureTarget.Texture2DArray, 0, PixelInternalFormat.Rgba, width, height, numOfLayers, 0,
                PixelFormat.Bgra, PixelType.UnsignedByte, IntPtr.Zero);

            for (int i = 0; i < images.Count; i++)
            {
                Bitmap image = images[i];

                BitmapData data = image.LockBits(new System.Drawing.Rectangle(0, 0, image.Width, image.Height),
                    ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                GL.TexSubImage3D(TextureTarget.Texture2DArray, 0, 0, 0, i, width, height, 1, PixelFormat.Bgra, PixelType.UnsignedByte, data.Scan0);

                image.UnlockBits(data);
            }

            if (generateMipmaps) GL.GenerateMipmap(GenerateMipmapTarget.Texture2DArray);

            Debug.Log($"Texture array '{name}' loaded");

            return new Texture2DArray(handle, name, width, height, generateMipmaps);
        }

        public Texture2DArray(int handle, string name, int width, int height, bool generateMipmaps = true)
        {
            mipmapLevels = generateMipmaps == false ? (byte)1 : (byte)Math.Floor(Math.Log(Math.Max(width, height), 2));

            base.handle = handle;
            base.name = name;
        }

        public override void Use(TextureUnit unit)
        {
            GL.ActiveTexture(unit);
            GL.BindTexture(TextureTarget.Texture2DArray, handle);
        }
    }
}
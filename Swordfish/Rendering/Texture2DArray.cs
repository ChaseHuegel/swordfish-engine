using System;
using System.Drawing;
using System.Drawing.Imaging;
using OpenTK.Graphics.OpenGL4;
using PixelFormat = OpenTK.Graphics.OpenGL4.PixelFormat;
using Swordfish;
using System.IO;
using System.Collections.Generic;

namespace Swordfish.Rendering
{
    public class Texture2DArray
    {
        public readonly string Name;
        public readonly int Handle;
        public readonly byte MipmapLevels;

        public static readonly float MaxAniso;
        static Texture2DArray() { MaxAniso = GL.GetFloat((GetPName)0x84FF); }

        public static Texture2DArray CreateFromFolder(string path, string name, int width, int height, bool generateMipmaps = true)
        {
            int handle = GL.GenTexture();
            int numOfLayers = 0;

            Debug.Log($"Loading texture array '{name}' from '{path}'");

            List<Bitmap> images = new List<Bitmap>();
            DirectoryInfo directory = new DirectoryInfo(path);
            foreach (FileInfo file in directory.GetFiles("*.png"))
            {
                //  TODO make sure this is a valid texture
                images.Add(new Bitmap(file.FullName));

                Debug.Log($"    Found texture '{file.Name}' at {numOfLayers}");

                //  Increase # of layers if this was a valid texture
                numOfLayers++;
            }

            Debug.Log($"    ...Texture array layers: {numOfLayers}");

            GL.BindTexture(TextureTarget.Texture2DArray, handle);

            GL.TexImage3D(TextureTarget.Texture2DArray, 0, PixelInternalFormat.Rgba, width, height, numOfLayers, 0,
                PixelFormat.Bgra, PixelType.UnsignedByte, IntPtr.Zero);

            Debug.Log($"...Building texture array '{name}'");
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
            MipmapLevels = generateMipmaps == false ? (byte)1 : (byte)Math.Floor(Math.Log(Math.Max(width, height), 2));

            Handle = handle;
            Name = name;
        }

        public void Use(TextureUnit unit)
        {
            GL.ActiveTexture(unit);
            GL.BindTexture(TextureTarget.Texture2DArray, Handle);
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
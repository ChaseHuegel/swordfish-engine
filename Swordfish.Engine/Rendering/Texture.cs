using System;

using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace Swordfish.Engine.Rendering
{
    public enum TextureCoordinate
    {
        S = TextureParameterName.TextureWrapS,
        T = TextureParameterName.TextureWrapT,
        R = TextureParameterName.TextureWrapR
    }

    public class Texture
    {
        protected string name;
        protected byte mipmapLevels;
        protected Vector2 size;

        private int _handle;
        protected int handle {
            get => _handle;
            set { _handle = value; Initialize(); }
        }

        public string GetName() => name;
        public int GetHandle() => handle;
        public IntPtr GetIntPtr() => (IntPtr)handle;
        public byte GetMipmapLevels() => mipmapLevels;
        public Vector2 GetSize() => size;

        public static readonly float MaxAniso;
        static Texture() { MaxAniso = GL.GetFloat((GetPName)0x84FF); }

        private void Initialize()
        {
            //  Default to pixelated filtering
            SetMinFilter(TextureMinFilter.Nearest);
            SetMagFilter(TextureMagFilter.Nearest);
            SetWrap(TextureCoordinate.S, TextureWrapMode.ClampToEdge);
        }

        public virtual void Use(TextureUnit unit)
        {
            GL.ActiveTexture(unit);
            GL.BindTexture(TextureTarget.Texture2D, handle);
        }

        public virtual void SetMinFilter(TextureMinFilter filter)
        {
            GL.TextureParameter(handle, TextureParameterName.TextureMinFilter, (int)filter);
        }

        public virtual void SetMagFilter(TextureMagFilter filter)
        {
            GL.TextureParameter(handle, TextureParameterName.TextureMagFilter, (int)filter);
        }

        public virtual void SetAnisotropy(float level)
        {
            const TextureParameterName TEXTURE_MAX_ANISOTROPY = (TextureParameterName)0x84FE;
            GL.TextureParameter(handle, TEXTURE_MAX_ANISOTROPY, Math.Clamp(level, 1, MaxAniso));
        }

        public virtual void SetLod(int @base, int min, int max)
        {
            GL.TextureParameter(handle, TextureParameterName.TextureLodBias, @base);
            GL.TextureParameter(handle, TextureParameterName.TextureMinLod, min);
            GL.TextureParameter(handle, TextureParameterName.TextureMaxLod, max);
        }

        public virtual void SetWrap(TextureCoordinate coord, TextureWrapMode mode)
        {
            GL.TextureParameter(handle, (TextureParameterName)coord, (int)mode);
        }

        public virtual void Dispose()
        {
            GL.DeleteTexture(handle);
        }
    }
}
using Swordfish.Rendering;

namespace Swordfish.Types
{
    public enum Shaders
    {
        DEFAULT,
        UNLIT,
        FLAT,

        PBR,
        PBR_ARRAY,

        POST,
        BLUR
    }

    internal static class ShadersExtensions
    {
        public static Shader DEFAULT = Shader.LoadFromFile("shaders/default.vert", "shaders/default.frag", "default");
        public static Shader UNLIT = Shader.LoadFromFile("shaders/unlit.vert", "shaders/unlit.frag", "unlit");
        public static Shader FLAT = Shader.LoadFromFile("shaders/flat.vert", "shaders/flat.frag", "flat");

        public static Shader PBR = Shader.LoadFromFile("shaders/pbr.vert", "shaders/pbr.frag", "pbr");
        public static Shader PBR_ARRAY = Shader.LoadFromFile("shaders/pbr_array.vert", "shaders/pbr_array.frag", "pbr array");

        public static Shader POST = Shader.LoadFromFile("shaders/post/post.vert", "shaders/post/post.frag", "post");
        public static Shader BLUR = Shader.LoadFromFile("shaders/post/post.vert", "shaders/post/blur.frag", "blur");

        public static Shader Get(this Shaders shader)
        {
            switch (shader)
            {
                case Shaders.DEFAULT: return DEFAULT;
                case Shaders.UNLIT: return UNLIT;
                case Shaders.FLAT: return FLAT;

                case Shaders.PBR: return PBR;
                case Shaders.PBR_ARRAY: return PBR_ARRAY;

                case Shaders.POST: return POST;
                case Shaders.BLUR: return BLUR;

                default: return null;
            }
        }
    }
}

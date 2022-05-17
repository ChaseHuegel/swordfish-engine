namespace Swordfish.Engine.Rendering
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

    public static class ShadersExtensions
    {
        public static Shader DEFAULT = Shader.LoadFromFile($"{Directories.SHADERS}/default.vert", $"{Directories.SHADERS}/default.frag", "default");
        public static Shader UNLIT = Shader.LoadFromFile($"{Directories.SHADERS}/unlit.vert", $"{Directories.SHADERS}/unlit.frag", "unlit");
        public static Shader FLAT = Shader.LoadFromFile($"{Directories.SHADERS}/flat.vert", $"{Directories.SHADERS}/flat.frag", "flat");

        public static Shader PBR = Shader.LoadFromFile($"{Directories.SHADERS}/pbr.vert", $"{Directories.SHADERS}/pbr.frag", "pbr");
        public static Shader PBR_ARRAY = Shader.LoadFromFile($"{Directories.SHADERS}/pbr_array.vert", $"{Directories.SHADERS}/pbr_array.frag", "pbr array");

        public static Shader POST = Shader.LoadFromFile($"{Directories.SHADERS}/post/post.vert", $"{Directories.SHADERS}/post/post.frag", "post");
        public static Shader BLUR = Shader.LoadFromFile($"{Directories.SHADERS}/post/post.vert", $"{Directories.SHADERS}/post/blur.frag", "blur");

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

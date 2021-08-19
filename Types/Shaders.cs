using Swordfish.Rendering;

namespace Swordfish.Types
{
    public enum Shaders
    {
        DEFAULT,
        UNLIT,
        PBR,
        PBR_ARRAY,
        POSTPROCESSING
    }

    internal static class ShadersExtensions
    {
        public static Shader DEFAULT = Shader.LoadFromFile("shaders/default.vert", "shaders/default.frag", "default");
        public static Shader UNLIT = Shader.LoadFromFile("shaders/unlit.vert", "shaders/unlit.frag", "unlit");
        public static Shader PBR = Shader.LoadFromFile("shaders/pbr.vert", "shaders/pbr.frag", "pbr");
        public static Shader PBR_ARRAY = Shader.LoadFromFile("shaders/pbr_array.vert", "shaders/pbr_array.frag", "pbr array");
        public static Shader POSTPROCESSING = Shader.LoadFromFile("shaders/postprocessing.vert", "shaders/postprocessing.frag", "postprocessing");

        public static Shader Get(this Shaders shader)
        {
            switch (shader)
            {
                case Shaders.DEFAULT: return DEFAULT;
                case Shaders.UNLIT: return UNLIT;
                case Shaders.PBR: return PBR;
                case Shaders.PBR_ARRAY: return PBR_ARRAY;
                case Shaders.POSTPROCESSING: return POSTPROCESSING;

                default: return null;
            }
        }
    }
}

using Swordfish.Rendering;

namespace Swordfish.Types
{
    public enum Icons
    {
        LIGHT,
    }

    internal static class IconsExtensions
    {
        public static Texture2D LIGHT = Texture2D.LoadFromFile("resources/icons/light.png", "ico_light");

        public static Texture2D Get(this Icons icon)
        {
            switch (icon)
            {
                case Icons.LIGHT: return LIGHT;

                default: return null;
            }
        }
    }
}
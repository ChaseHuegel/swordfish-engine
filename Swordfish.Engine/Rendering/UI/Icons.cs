namespace Swordfish.Engine.Rendering.UI
{
    public enum Icons
    {
        LIGHT,
    }

    public static class IconsExtensions
    {
        public static Texture2D LIGHT = Texture2D.LoadFromFile($"{Directories.ICONS}/light.png", "ico_light");

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

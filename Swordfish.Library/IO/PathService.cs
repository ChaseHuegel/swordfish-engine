using System.Reflection;

namespace Swordfish.Library.IO
{
    public class PathService : IPathService
    {
        private const string PLUGINS_FOLDER = "plugins";
        private const string MODS_FOLDER = "mods";
        private const string CONFIG_FOLDER = "config";
        private const string SCREENSHOTS_FOLDER = "screenshots";
        private const string SHADERS_FOLDER = "shaders";
        private const string UI_FOLDER = "ui";
        private const string RESOURCES_FOLDER = "resources";
        private const string FONTS_FOLDER = "fonts";
        private const string ICONS_FOLDER = "icons";
        private const string MODELS_FOLDER = "models";
        private const string TEXTURES_FOLDER = "textures";

        public PathInfo Root { get; protected set; }
        public PathInfo Plugins => Root.At(PLUGINS_FOLDER).CreateDirectory();
        public PathInfo Mods => Root.At(MODS_FOLDER).CreateDirectory();
        public PathInfo Config => Root.At(CONFIG_FOLDER).CreateDirectory();
        public PathInfo Screenshots => Root.At(SCREENSHOTS_FOLDER).CreateDirectory();
        public PathInfo Shaders => Root.At(SHADERS_FOLDER).CreateDirectory();
        public PathInfo UI => Root.At(UI_FOLDER).CreateDirectory();
        public PathInfo Resources => Root.At(RESOURCES_FOLDER).CreateDirectory();
        public PathInfo Fonts => Resources.At(FONTS_FOLDER).CreateDirectory();
        public PathInfo Icons => Resources.At(ICONS_FOLDER).CreateDirectory();
        public PathInfo Models => Resources.At(MODELS_FOLDER).CreateDirectory();
        public PathInfo Textures => Resources.At(TEXTURES_FOLDER).CreateDirectory();

        public PathService()
        {
            var assembly = Assembly.GetExecutingAssembly();
            string directory = System.IO.Path.GetDirectoryName(assembly.Location);
            Root = new PathInfo(directory);
        }
        
        public PathService(string absolutePath)
        {
            Root = new PathInfo(absolutePath);
        }
    }
}

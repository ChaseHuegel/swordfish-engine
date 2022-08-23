using System;
using System.IO;

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
        private const string RESOURCES_FOLDER = "plugins";
        private const string FONTS_FOLDER = "fonts";
        private const string ICONS_FOLDER = "icons";
        private const string MODELS_FOLDER = "models";
        private const string TEXTURES_FOLDER = "textures";

        public IPath Root => root ?? (root = new Path(AppDomain.CurrentDomain.BaseDirectory));

        public IPath Plugins => plugins ?? (plugins = Root.At(PLUGINS_FOLDER).CreateDirectory());

        public IPath Mods => mods ?? (mods = Root.At(MODS_FOLDER).CreateDirectory());

        public IPath Config => config ?? (config = Root.At(CONFIG_FOLDER).CreateDirectory());

        public IPath Screenshots => screenshots ?? (screenshots = Root.At(SCREENSHOTS_FOLDER).CreateDirectory());

        public IPath Shaders => shaders ?? (shaders = Root.At(SHADERS_FOLDER).CreateDirectory());

        public IPath UI => ui ?? (ui = Root.At(UI_FOLDER).CreateDirectory());

        public IPath Resources => resources ?? (resources = Root.At(RESOURCES_FOLDER).CreateDirectory());

        public IPath Fonts => fonts ?? (fonts = Resources.At(FONTS_FOLDER).CreateDirectory());

        public IPath Icons => icons ?? (icons = Resources.At(ICONS_FOLDER).CreateDirectory());

        public IPath Models => models ?? (models = Resources.At(MODELS_FOLDER).CreateDirectory());

        public IPath Textures => textures ?? (textures = Resources.At(TEXTURES_FOLDER).CreateDirectory());

        private IPath root;
        private IPath plugins;
        private IPath mods;
        private IPath config;
        private IPath screenshots;
        private IPath shaders;
        private IPath ui;
        private IPath resources;
        private IPath fonts;
        private IPath icons;
        private IPath models;
        private IPath textures;
    }
}

using System.IO;
using System;

namespace Swordfish.Engine
{
    public static class Directories
    {
        public static string CURRENT => Directory.GetCurrentDirectory();

        public static string CONFIG = "config/";
        public static string SCREENSHOTS = "screenshots/";
        public static string SHADERS = "shaders/";
        public static string RESOURCES = "resources/";
        public static string FONTS = $"{RESOURCES}/fonts/";
        public static string ICONS = $"{RESOURCES}/icons/";
        public static string MODELS = $"{RESOURCES}/models/";
        public static string TEXTURES = $"{RESOURCES}/textures/";
    }
}

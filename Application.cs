using System;
using Swordfish;
using OpenTK;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Desktop;
using WindowBorder = OpenTK.Windowing.Common.WindowBorder;
using Swordfish.Rendering;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System.Globalization;

namespace waywardbeyond
{
    class Application
    {
        private const string TITLE = "Wayward Beyond";
        private const int FRAMELIMIT = 60;

        public static Game MainWindow;

        static void Main(string[] args)
        {
            MonitorInfo monitor = GLHelper.GetPrimaryDisplay();
            Vector2i size = new Vector2i(monitor.HorizontalResolution, monitor.VerticalResolution);

            var nativeWindowSettings = new NativeWindowSettings()
            {
                Size = size,
                Title = TITLE,
                WindowBorder = WindowBorder.Hidden
            };

            using (Game game = new Game(GameWindowSettings.Default, nativeWindowSettings))
            {
                MainWindow = game;
                game.RenderFrequency = FRAMELIMIT;
                game.Run();
            }
        }
    }
}

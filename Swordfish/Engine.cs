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
using Swordfish.Physics;
using System.ComponentModel;

namespace Swordfish
{
    class Engine
    {
        public static WindowContext MainWindow;
        public static RenderContext Renderer;
        public static PhysicsContext Physics;
        public static CoreSettings Settings;

        static void Main(string[] args)
        {
            MonitorInfo monitor = GLHelper.GetPrimaryDisplay();
            Vector2i size = new Vector2i(monitor.HorizontalResolution, monitor.VerticalResolution);

            Renderer = new RenderContext();
            Physics = new PhysicsContext();
            Settings = new CoreSettings();

            var nativeWindowSettings = new NativeWindowSettings()
            {
                Size = size,
                Title = Settings.WINDOW_TITLE,
                WindowBorder = Settings.WINDOW_BORDER
            };

            using (WindowContext window = new WindowContext(GameWindowSettings.Default, nativeWindowSettings))
            {
                MainWindow = window;
                window.RenderFrequency = Settings.FRAMELIMIT;
                window.Run();
            }
        }
    }
}

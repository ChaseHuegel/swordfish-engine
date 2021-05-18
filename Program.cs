using System;
using Swordfish;
using OpenTK;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Desktop;
using WindowBorder = OpenTK.Windowing.Common.WindowBorder;

namespace waywardbeyond
{
    class Program
    {
        private const string TITLE = "Wayward Beyond";
        private const int FRAMELIMIT = 60;

        static void Main(string[] args)
        {
            OpenTK.Windowing.Desktop.Monitors.TryGetMonitorInfo(0, out MonitorInfo monitor);
            Vector2i size = new Vector2i(monitor.HorizontalResolution, monitor.VerticalResolution);

            var nativeWindowSettings = new NativeWindowSettings()
            {
                Size = size,
                Title = TITLE,
                WindowBorder = WindowBorder.Hidden
            };

            using (var window = new Game(GameWindowSettings.Default, nativeWindowSettings))
            {
                window.RenderFrequency = FRAMELIMIT;
                window.Run();
            }
        }
    }
}

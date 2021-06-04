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
using Swordfish.ECS_OLD;

namespace Swordfish
{
    public class Engine
    {
        public static float FrameTime = 0f;
        public static float DeltaTime = 0f;
        public static int Frame = 0;

        public static EcsContext ECS;
        public static WindowContext MainWindow;
        public static RenderContext Renderer;
        public static PhysicsContext Physics;
        public static CoreSettings Settings;

        public static Action StartCallback;
        public static Action StopCallback;
        public static Action ShutdownCallback;
        public static Action UpdateCallback;
        public static Action PreRenderCallback;
        public static Action PostRenderCallback;
        public static Action GuiCallback;

        public static void Initialize()
        {
            MonitorInfo screen = GLHelper.GetPrimaryDisplay();
            Vector2i screenSize = new Vector2i(screen.HorizontalResolution, screen.VerticalResolution);

            Renderer = new RenderContext();
            Physics = new PhysicsContext();
            Settings = new CoreSettings();

            var nativeWindowSettings = new NativeWindowSettings()
            {
                Title = Settings.WINDOW_TITLE,
                Size = Settings.WINDOW_FULLSCREEN ? screenSize : Settings.WINDOW_SIZE,
                WindowBorder = Settings.WINDOW_FULLSCREEN ? WindowBorder.Hidden : WindowBorder.Fixed
            };

            using (WindowContext window = new WindowContext(GameWindowSettings.Default, nativeWindowSettings))
            {
                MainWindow = window;
                window.RenderFrequency = Settings.FRAMELIMIT;
                window.Run();
            }
        }

        public static void Start()
        {
            ECS = new EcsContext();
            ECS.Start();

            StartCallback?.Invoke();
        }

        public static void Step()
        {
            ECS.Step();

            UpdateCallback?.Invoke();
        }

        public static void Shutdown()
        {
            ECS.Shutdown();

            ShutdownCallback?.Invoke();
            MainWindow.Close();
        }
    }
}

using System;
using OpenTK.Mathematics;
using OpenTK.Windowing.Desktop;
using WindowBorder = OpenTK.Windowing.Common.WindowBorder;
using Swordfish.Rendering;
using Swordfish.Physics;
using Swordfish.ECS;

namespace Swordfish
{
    public class Engine
    {
        public static float DeltaTime = 0f;
        public static float ECSTime = 0f;
        public static float FrameTime = 0f;
        public static int Frame = 0;

        public static WindowContext MainWindow;
        public static RenderContext Renderer;
        public static PhysicsContext Physics;
        public static CoreSettings Settings;
        public static ECSContext ECS;
        public static Random Random;

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

            Settings = new CoreSettings();
            Renderer = new RenderContext();
            Physics = new PhysicsContext();
            ECS = new ECSContext();
            Random = new Random();

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

            //  Dump the log if this isn't a release build
            if (!Engine.Settings.IS_RELEASE) Debug.Dump();
        }

        public static void Start()
        {
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

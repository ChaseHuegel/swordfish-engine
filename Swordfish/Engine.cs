using System;
using OpenTK.Mathematics;
using OpenTK.Windowing.Desktop;
using WindowBorder = OpenTK.Windowing.Common.WindowBorder;
using Swordfish.Rendering;
using Swordfish.Physics;
using Swordfish.ECS;
using source;
using OpenTK.Windowing.Common;

namespace Swordfish
{
    public class Engine
    {
        public static float DeltaTime = 0f;
        public static float ECSTime = 0f;
        public static float FrameTime = 0f;
        public static int Frame = 0;

        private static bool IsSafeShutdown = false;

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
            Debug.Initialize();

            //  Load engine settings
            Settings = CoreSettings.LoadConfig("swordfish.toml");

            //  Initialize all engine members
            Renderer = new RenderContext();
            Physics = new PhysicsContext();
            ECS = new ECSContext();
            Random = new Random();

            MonitorInfo screen = GLHelper.GetPrimaryDisplay();
            Vector2i screenSize = new Vector2i(screen.HorizontalResolution, screen.VerticalResolution);

            var nativeWindowSettings = new NativeWindowSettings()
            {
                Title = Settings.Window.TITLE,
                Size = Settings.Window.FULLSCREEN ? screenSize : Settings.Window.SIZE,
                WindowBorder = Settings.Window.FULLSCREEN ? WindowBorder.Hidden : WindowBorder.Fixed
            };

            using (WindowContext window = new WindowContext(GameWindowSettings.Default, nativeWindowSettings))
            {
                MainWindow = window;
                window.RenderFrequency = Settings.Renderer.FRAMECAP;
                window.VSync = Settings.Renderer.VSYNC;
                window.Run();
            }

            //  Attempt to dump the log
            TryDumpLog();
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
            MainWindow.Close();

            ShutdownCallback?.Invoke();
            ECS.Shutdown();

            IsSafeShutdown = true;
        }

        private static void TryDumpLog()
        {
            //  If this is a safe shutdown...
            if (IsSafeShutdown)
            {
                //  Dump the log if this this is a debug build OR any errors were detected
                #if DEBUG
                    Debug.Dump();
                #else
                    if (Debug.HasErrors) Debug.Dump();
                #endif
            }
            //  ...otherwise an unsafe shutdown
            else
            {
                //  Notify and dump the log
                Debug.Log("Unexpected shutdown! Did it crash?");
                Debug.Dump();

                //  Perform shutdown steps
                Shutdown();
            }
        }
    }
}

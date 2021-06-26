using System;

using OpenTK.Mathematics;
using OpenTK.Windowing.Desktop;

using Swordfish.Diagnostics;
using Swordfish.ECS;
using Swordfish.Physics;
using Swordfish.Rendering;

using WindowBorder = OpenTK.Windowing.Common.WindowBorder;

namespace Swordfish
{
    public class Engine
    {
        private static float _timescale = 1f;
        public static float Timescale
        {
            get => _timescale;
            set => _timescale = Math.Clamp(value, 0f, 100f);
        }

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

        /// <summary>
        /// A static instance of System.Random that should only be used in the Main thread
        /// </summary>
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
                Size = Settings.Window.FULLSCREEN ? screenSize : new Vector2i(Settings.Window.WIDTH, Settings.Window.HEIGHT),
                WindowBorder = Settings.Window.FULLSCREEN ? WindowBorder.Hidden : WindowBorder.Fixed
            };

            using (WindowContext window = new WindowContext(GameWindowSettings.Default, nativeWindowSettings))
            {
                MainWindow = window;
                window.RenderFrequency = Settings.Window.FRAMELIMIT;
                window.UpdateFrequency = Settings.Window.UPDATELIMIT;
                window.VSync = Settings.Window.VSYNC;
                window.Run();
            }

            //  Attempt to dump the log
            TryDumpLog();
        }

        public static void Start()
        {
            ECS.Start();
            Physics.Start();

            StartCallback?.Invoke();
        }

        public static void Step()
        {
            UpdateCallback?.Invoke();
        }

        public static void Shutdown()
        {
            MainWindow.Close();

            ShutdownCallback?.Invoke();

            ECS.Shutdown();
            Physics.Shutdown();

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

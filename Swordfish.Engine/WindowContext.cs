using System;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

using Swordfish.Library.Diagnostics;
using Swordfish.Library.Util;
using Swordfish.Engine.Rendering;

namespace Swordfish.Engine
{
    public class WindowContext : GameWindow
    {
        public int FPS { get; private set; }
        public float DeltaTime { get; private set; }

        private float[] frameTimes = new float[6];
        private int frameTimeIndex = 0;
        private float frameTimer = 0f;

        public WindowContext(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings) : base(gameWindowSettings, nativeWindowSettings)
        {
        }

        protected override void OnLoad()
        {
            MonitorInfo screen = GLHelper.GetPrimaryDisplay();

            Debug.Log($"Started {(Swordfish.Settings.Window.TITLE)}");
            Debug.Log($"Device: {screen.HorizontalResolution}x{screen.VerticalResolution}", LogType.CONTINUED);
            Debug.Log($"Window: {(Swordfish.Settings.Window.WIDTH)}x{(Swordfish.Settings.Window.HEIGHT)}", LogType.CONTINUED);
            Debug.Log($"Fullscreen: {(Swordfish.Settings.Window.FULLSCREEN)}", LogType.CONTINUED);
            Debug.Log($"Vsync: {(Swordfish.Settings.Window.VSYNC)}", LogType.CONTINUED);
            Debug.Log($"Framelimit: {(Swordfish.Settings.Window.FRAMELIMIT)}", LogType.CONTINUED);
            Debug.Log($"Updatelimit: {(Swordfish.Settings.Window.UPDATELIMIT)}", LogType.CONTINUED);

            Swordfish.Renderer.Initialize();

            Swordfish.Start();

            //  Manual fallback to grab GL errors if debug output is unavailable
            Debug.TryCollectAllGLErrors("Load");

            base.OnLoad();
        }

        protected override void OnUnload()
        {
            Swordfish.Renderer.Dispose();
            Swordfish.StopCallback?.Invoke();

            //  Manual fallback to grab GL errors if debug output is unavailable
            Debug.TryCollectAllGLErrors("Unload");

            base.OnUnload();
        }

        protected override void OnUpdateFrame(FrameEventArgs e)
        {
            DeltaTime = (float)e.Time;
            Swordfish.DeltaTime = DeltaTime;

            //  TODO: Very quick and dirty stable timing
            frameTimer += DeltaTime;
            frameTimes[frameTimeIndex] = DeltaTime;
            frameTimeIndex++;
            if (frameTimeIndex >= frameTimes.Length)
                frameTimeIndex = 0;
            if (frameTimer >= 1f/frameTimes.Length)
            {
                frameTimer = 0f;

                float highest = 0f;
                float lowest = 9999f;
                Swordfish.FrameTime = 0f;
                foreach (float timing in frameTimes)
                {
                    Swordfish.FrameTime += timing;
                    if (timing <= lowest) lowest = timing;
                    if (timing >= highest) highest = timing;
                }

                Swordfish.FrameTime -= lowest;
                Swordfish.FrameTime -= highest;
                Swordfish.FrameTime /= (frameTimes.Length - 2);
            }

            //  Calculate FPS and cap it by the window's FPS cap
            FPS = (int)(1f / Swordfish.FrameTime);
            if (Swordfish.MainWindow.RenderFrequency > 0 && FPS > Swordfish.MainWindow.RenderFrequency)
                FPS = (int)Swordfish.MainWindow.RenderFrequency;

            //  Apply timescale
            Swordfish.DeltaTime *= Swordfish.Timescale;

            //  Allow fullscreen toggle with ALT+ENTER
            if (Input.IsKeyPressed(Keys.Enter) && Input.IsKeyDown(Keys.LeftAlt))
            {
                //  Toggle fullscreen
                Swordfish.Settings.Window.FULLSCREEN = !Swordfish.Settings.Window.FULLSCREEN;

                //  Update state
                WindowState = Swordfish.Settings.Window.FULLSCREEN ? WindowState.Fullscreen : WindowState.Normal;

                OnResize(new ResizeEventArgs(ClientSize));
            }

            //  Screenshot the render with F11
            if (Input.IsKeyPressed(Keys.F11))
            {
                Directory.CreateDirectory(Directories.SCREENSHOTS);

                //  Screenshots are formatted year.month.day-N where Nth screenshot on that date
                string path = Directories.SCREENSHOTS
                            + DateTime.Now.ToString("yyyy.MM.dd") + "-"
                            + (Directory.GetFiles(Directories.SCREENSHOTS, $"{DateTime.Now.ToString("yyyy.MM.dd")}*").ToArray().Length + 1)
                            + ".png";

                Swordfish.Renderer.Screenshot(false).Save(path, ImageFormat.Png);
                Debug.Log($"Saved screenshot '{path}'");
            }

            //  Screenshot the window with F12
            if (Input.IsKeyPressed(Keys.F12))
            {
                Directory.CreateDirectory(Directories.SCREENSHOTS);

                //  Screenshots are formatted year.month.day-N where Nth screenshot on that date
                string path = Directories.SCREENSHOTS
                            + DateTime.Now.ToString("yyyy.MM.dd") + "-"
                            + (Directory.GetFiles(Directories.SCREENSHOTS, $"{DateTime.Now.ToString("yyyy.MM.dd")}*").ToArray().Length + 1)
                            + ".png";

                Swordfish.Renderer.Screenshot().Save(path, ImageFormat.Png);
                Debug.Log($"Saved screenshot '{path}'");
            }

            Swordfish.Step();

            //  Manual fallback to grab GL errors if debug output is unavailable
            Debug.TryCollectGLError("UpdateFrame");

            base.OnUpdateFrame(e);
        }

        protected override void OnRenderFrame(FrameEventArgs e)
        {
            Swordfish.Frame++;

            Swordfish.PreRenderCallback?.Invoke();
            Swordfish.Renderer.Render();
            Swordfish.PostRenderCallback?.Invoke();

            //  Manual fallback to grab GL errors if debug output is unavailable
            Debug.TryCollectGLError("RenderFrame");

            Context.SwapBuffers();
            base.OnRenderFrame(e);
        }

        protected override void OnResize(ResizeEventArgs e)
        {
            GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);
            Swordfish.Renderer.OnWindowResized();

            //  Update size in settings
            Swordfish.Settings.Window.WIDTH = ClientSize.X;
            Swordfish.Settings.Window.HEIGHT = ClientSize.Y;

            //  Manual fallback to grab GL errors if debug output is unavailable
            Debug.TryCollectGLError("Resize");

            base.OnResize(e);
        }

        protected override void OnTextInput(TextInputEventArgs e)
        {
            base.OnTextInput(e);

            Swordfish.Renderer.GuiController.PressChar((char)e.Unicode);
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);

            Swordfish.Renderer.GuiController.MouseScroll(e.Offset);
        }
    }
}

using System.Linq;
using System.Drawing.Imaging;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

using Swordfish.Diagnostics;
using System.IO;
using System;

namespace Swordfish
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
            Debug.Log($"Started {this.Title}");
            Debug.Log($"    Framelimit {this.RenderFrequency}", LogType.NONE);
            Debug.Log($"    Resolution {ClientSize.X} x {ClientSize.Y} borderless", LogType.NONE);

            Engine.Renderer.Load();

            Engine.Start();

            //  Manual fallback to grab GL errors if debug output is unavailable
            Debug.TryCollectAllGLErrors("Load");

            base.OnLoad();
        }

        protected override void OnUnload()
        {
            Engine.Renderer.Unload();
            Engine.StopCallback?.Invoke();

            //  Manual fallback to grab GL errors if debug output is unavailable
            Debug.TryCollectAllGLErrors("Unload");

            base.OnUnload();
        }

        protected override void OnUpdateFrame(FrameEventArgs e)
        {
            DeltaTime = (float)e.Time;
            Engine.DeltaTime = DeltaTime;
            Engine.Frame++;

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
                Engine.FrameTime = 0f;
                foreach (float timing in frameTimes)
                {
                    Engine.FrameTime += timing;
                    if (timing <= lowest) lowest = timing;
                    if (timing >= highest) highest = timing;
                }

                Engine.FrameTime -= lowest;
                Engine.FrameTime -= highest;
                Engine.FrameTime /= (frameTimes.Length - 2);
            }

            //  Calculate FPS and cap it by the window's FPS cap
            FPS = (int)(1f / Engine.FrameTime);
            if (Engine.MainWindow.RenderFrequency > 0 && FPS > Engine.MainWindow.RenderFrequency)
                FPS = (int)Engine.MainWindow.RenderFrequency;

            //  Apply timescale
            Engine.DeltaTime *= Engine.Timescale;

            //  Allow fullscreen toggle with ALT+ENTER
            if (Input.IsKeyPressed(Keys.Enter) && Input.IsKeyDown(Keys.LeftAlt))
            {
                //  Toggle fullscreen
                Engine.Settings.Window.FULLSCREEN = !Engine.Settings.Window.FULLSCREEN;

                //  Update state
                WindowState = Engine.Settings.Window.FULLSCREEN ? WindowState.Fullscreen : WindowState.Normal;

                OnResize(new ResizeEventArgs(ClientSize));
            }

            //  Screenshot the render with F11
            if (Input.IsKeyPressed(Keys.F11))
            {
                Directory.CreateDirectory("screenshots/");

                //  Screenshots are formatted year.month.day-N where Nth screenshot on that date
                string path = "screenshots/"
                            + DateTime.Now.ToString("yyyy.MM.dd") + "-"
                            + (Directory.GetFiles("screenshots/", $"{DateTime.Now.ToString("yyyy.MM.dd")}*").ToArray().Length + 1)
                            + ".png";

                Engine.Renderer.Screenshot(false).Save(path, ImageFormat.Png);
                Debug.Log($"Saved screenshot '{path}'");
            }

            //  Screenshot the window with F12
            if (Input.IsKeyPressed(Keys.F12))
            {
                Directory.CreateDirectory("screenshots/");

                //  Screenshots are formatted year.month.day-N where Nth screenshot on that date
                string path = "screenshots/"
                            + DateTime.Now.ToString("yyyy.MM.dd") + "-"
                            + (Directory.GetFiles("screenshots/", $"{DateTime.Now.ToString("yyyy.MM.dd")}*").ToArray().Length + 1)
                            + ".png";

                Engine.Renderer.Screenshot().Save(path, ImageFormat.Png);
                Debug.Log($"Saved screenshot '{path}'");
            }

            Engine.Step();

            //  Manual fallback to grab GL errors if debug output is unavailable
            Debug.TryCollectGLError("UpdateFrame");

            base.OnUpdateFrame(e);
        }

        protected override void OnRenderFrame(FrameEventArgs e)
        {
            Engine.PreRenderCallback?.Invoke();
                Engine.Renderer.Render();
            Engine.PostRenderCallback?.Invoke();

            //  Manual fallback to grab GL errors if debug output is unavailable
            Debug.TryCollectGLError("RenderFrame");

            Context.SwapBuffers();
            base.OnRenderFrame(e);
        }

        protected override void OnResize(ResizeEventArgs e)
        {
            GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);
            Engine.Renderer.GuiController.WindowResized(ClientSize.X, ClientSize.Y);

            //  Update size in settings
            Engine.Settings.Window.WIDTH = ClientSize.X;
            Engine.Settings.Window.HEIGHT = ClientSize.Y;

            //  Manual fallback to grab GL errors if debug output is unavailable
            Debug.TryCollectGLError("Resize");

            base.OnResize(e);
        }

        protected override void OnTextInput(TextInputEventArgs e)
        {
            base.OnTextInput(e);

            Engine.Renderer.GuiController.PressChar((char)e.Unicode);
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);

            Engine.Renderer.GuiController.MouseScroll(e.Offset);
        }
    }
}

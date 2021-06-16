using System;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Swordfish.Rendering;
using Image = OpenTK.Windowing.Common.Input.Image;
using System.Drawing;
using System.Reflection.Metadata.Ecma335;
using System.Threading;

namespace Swordfish
{
    public class WindowContext : GameWindow
    {
        public int FPS { get; private set; }

        private float[] frameTimes = new float[6];
        private int frameTimeIndex = 0;
        private float frameTimer = 0f;

        public WindowContext(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings) : base(gameWindowSettings, nativeWindowSettings)
        {
        }

        protected override void OnLoad()
        {
            Debug.Log($"Started {this.Title}");
            Debug.Log("Settings");
            Debug.Log($"    Framelimit {this.RenderFrequency}");
            Debug.Log($"    Resolution {ClientSize.X} x {ClientSize.Y} borderless");

            Engine.Renderer.Load();

            Engine.Start();

            base.OnLoad();
        }

        protected override void OnUnload()
        {
            Engine.Renderer.Unload();
            Engine.StopCallback?.Invoke();

            base.OnUnload();
        }

        protected override void OnUpdateFrame(FrameEventArgs e)
        {
            Engine.DeltaTime = (float)e.Time;
            Engine.Frame++;

            //  TODO: Very quick and dirty stable timing
            frameTimer += Engine.DeltaTime;
            frameTimes[frameTimeIndex] = Engine.DeltaTime;
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

            //  Allow fullscreen toggle with ALT+ENTER
            if (Input.IsKeyPressed(Keys.Enter) && Input.IsKeyDown(Keys.LeftAlt))
            {
                //  Toggle fullscreen
                Engine.Settings.Window.FULLSCREEN = !Engine.Settings.Window.FULLSCREEN;

                //  Update state
                WindowState = Engine.Settings.Window.FULLSCREEN ? WindowState.Fullscreen : WindowState.Normal;

                //  Update window size
                Engine.Settings.Window.SIZE = ClientSize;

                OnResize(new ResizeEventArgs(ClientSize));
            }

            Engine.Step();

            base.OnUpdateFrame(e);
        }

        protected override void OnRenderFrame(FrameEventArgs e)
        {
            Engine.PreRenderCallback?.Invoke();
                Engine.Renderer.Render();
            Engine.PostRenderCallback?.Invoke();

            //  Manual fallback to grab GL errors if debug output is unavailable
            Debug.TryLogGLError("OpenGL");

            Context.SwapBuffers();
            base.OnRenderFrame(e);
        }

        protected override void OnResize(ResizeEventArgs e)
        {
            GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);
            Engine.Renderer.GuiController.WindowResized(ClientSize.X, ClientSize.Y);

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

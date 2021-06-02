using System;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Swordfish.Rendering;
using Image = OpenTK.Windowing.Common.Input.Image;
using System.Drawing;
using System.Reflection.Metadata.Ecma335;

namespace Swordfish
{
    public class WindowContext : GameWindow
    {
        private float[] frameTimes = new float[60];
        private int frameTimeIndex = 0;
        private float frameTimer = 0f;

        public WindowContext(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings) : base(gameWindowSettings, nativeWindowSettings)
        {
        }

        protected override void OnLoad()
        {
            Console.SetOut(Debug.GetWriter());

            Debug.Log($"Started {this.Title}");
            Debug.Log("Settings");
            Debug.Log($"    Framelimit {this.RenderFrequency}");
            Debug.Log($"    Resolution {ClientSize.X} x {ClientSize.Y} borderless");

            Debug.Log($"OpenGL v{GL.GetString(StringName.Version)}");
            Debug.Log($"    Extensions found: {GLHelper.GetSupportedExtensions().Count}");
            Debug.Log($"    {GL.GetString(StringName.Vendor)} {GL.GetString(StringName.Renderer)}");

            Engine.Renderer.Load();
            Engine.StartCallback?.Invoke();

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

            //  TODO: Very quick and dirty
            frameTimer += Engine.DeltaTime;
            if (frameTimer >= 1f/frameTimes.Length)
            {
                frameTimer = 0f;

                frameTimes[frameTimeIndex] = Engine.DeltaTime;
                frameTimeIndex++;
                if (frameTimeIndex >= frameTimes.Length)
                    frameTimeIndex = 0;

                Engine.FrameTime = 0f;
                foreach (float timing in frameTimes)
                    Engine.FrameTime += timing;
                Engine.FrameTime /= frameTimes.Length;
            }

            //  Don't update if the window isn't in focus
            if (!IsFocused) return;

            //  Allow fullscreen toggle with ALT+ENTER
            if (Input.IsKeyPressed(Keys.Enter) && Input.IsKeyDown(Keys.LeftAlt))
            {
                Engine.Settings.WINDOW_FULLSCREEN = !Engine.Settings.WINDOW_FULLSCREEN;
                WindowState = Engine.Settings.WINDOW_FULLSCREEN ? WindowState.Fullscreen : WindowState.Normal;

                OnResize(new ResizeEventArgs(ClientSize));
            }

            Engine.UpdateCallback?.Invoke();

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
